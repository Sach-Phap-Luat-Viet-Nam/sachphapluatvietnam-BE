using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application.Storefront;

/// <summary>Truy vấn chỉ đọc cho website khách hàng: chỉ đầu sách <c>active</c> và ấn bản đã công bố.</summary>
internal sealed class StorefrontQueryService(CatalogDbContext dbContext, IInventoryService inventoryService)
{
    public async Task<PagedResponse<BookSummaryResponse>> ListBooksAsync(
        BooksQuery query,
        CancellationToken cancellationToken)
    {
        var books = PublishedBooks();
        books = await ApplyFiltersAsync(books, query, cancellationToken);

        var total = await books.CountAsync(cancellationToken);
        var page = await Sort(books, query.Sort)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(book => new
            {
                book.Id,
                book.Title,
                book.Slug,
                book.BookType,
                Latest = book.Editions
                    .Where(edition => edition.IsLatest)
                    .Select(edition => new
                    {
                        edition.EditionName,
                        edition.PublishYear,
                        edition.CoverImageUrl,
                        edition.UpdatedToText,
                        edition.Status,
                        Skus = edition.Skus.Select(sku => new { sku.Id, sku.ListPrice, sku.SalePrice }).ToList(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var skuIds = page.SelectMany(book => book.Latest?.Skus.Select(sku => sku.Id) ?? []).ToList();
        var stock = await inventoryService.GetAvailableQuantitiesAsync(skuIds, cancellationToken);

        var items = page.Select(book =>
        {
            var cheapest = book.Latest?.Skus.MinBy(sku => sku.SalePrice);
            return new BookSummaryResponse(
                book.Id,
                book.Title,
                book.Slug,
                book.BookType,
                book.Latest?.EditionName,
                book.Latest?.PublishYear,
                book.Latest?.CoverImageUrl,
                book.Latest?.UpdatedToText,
                book.Latest?.Status,
                cheapest?.SalePrice,
                cheapest?.ListPrice,
                cheapest is null ? null : Pricing.DiscountPercent(cheapest.ListPrice, cheapest.SalePrice),
                InStock: book.Latest?.Skus.Any(sku => stock.GetValueOrDefault(sku.Id) > 0) ?? false);
        }).ToList();

        return PagedResponse<BookSummaryResponse>.Create(items, query, total);
    }

    public async Task<BookDetailResponse> GetBookAsync(string slug, CancellationToken cancellationToken)
    {
        var book = await PublishedBooks()
            .Include(b => b.Categories)
            .Include(b => b.Authors)
            .Include(b => b.Editions.Where(edition => edition.Status != EditionStatus.Draft))
                .ThenInclude(edition => edition.Skus)
            .Include(b => b.Editions.Where(edition => edition.Status != EditionStatus.Draft))
                .ThenInclude(edition => edition.LegalDocuments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Slug == slug, cancellationToken)
            ?? throw CatalogErrors.NotFound("sách");

        var skuIds = book.Editions.SelectMany(edition => edition.Skus).Select(sku => sku.Id).ToList();
        var stock = await inventoryService.GetAvailableQuantitiesAsync(skuIds, cancellationToken);

        var editions = book.Editions
            .OrderByDescending(edition => edition.IsLatest)
            .ThenByDescending(edition => edition.ReleaseDate)
            .ThenByDescending(edition => edition.Id)
            .Select(edition => ToEditionResponse(edition, stock))
            .ToList();

        return new BookDetailResponse(
            book.Id,
            book.Title,
            book.Slug,
            book.Description,
            book.BookType,
            book.TargetAudiences,
            book.MetaTitle,
            book.MetaDescription,
            book.Categories.OrderBy(c => c.SortOrder).Select(c => new NamedReference(c.Id, c.Name, c.Slug)).ToList(),
            book.Authors.OrderBy(a => a.SortOrder).Select(a => new NamedReference(a.Id, a.Name, a.Slug)).ToList(),
            editions);
    }

    public async Task<IReadOnlyList<CategoryNode>> GetCategoryTreeAsync(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new { category.Id, category.Name, category.Slug, category.ParentId })
            .ToListAsync(cancellationToken);

        var byParent = categories.ToLookup(category => category.ParentId);
        var ids = categories.Select(category => category.Id).ToHashSet();

        List<CategoryNode> Build(long? parentId) => byParent[parentId]
            .Select(category => new CategoryNode(category.Id, category.Name, category.Slug, Build(category.Id)))
            .ToList();

        // Danh mục có cha đã bị xóa mềm được đưa lên gốc để không bị mất khỏi cây.
        var orphans = categories
            .Where(category => category.ParentId is { } parentId && !ids.Contains(parentId))
            .Select(category => new CategoryNode(category.Id, category.Name, category.Slug, Build(category.Id)));
        return [.. Build(null), .. orphans];
    }

    internal static EditionResponse ToEditionResponse(Edition edition, IReadOnlyDictionary<long, int> stock) => new(
        edition.Id,
        edition.EditionName,
        edition.Isbn,
        edition.Publisher,
        edition.PublishingDecisionNo,
        edition.RegistrationNo,
        edition.PublishYear,
        edition.UpdatedToText,
        edition.UpdatedToDate,
        edition.Pages,
        edition.Size,
        edition.WeightGram,
        edition.CoverImageUrl,
        edition.PreviewFileUrl,
        edition.TableOfContents,
        edition.ReleaseDate,
        edition.IsLatest,
        edition.Status,
        edition.Skus.OrderBy(sku => sku.CoverType).Select(sku => new SkuResponse(
            sku.Id,
            sku.SkuCode,
            sku.CoverType,
            sku.ListPrice,
            sku.SalePrice,
            Pricing.DiscountPercent(sku.ListPrice, sku.SalePrice),
            sku.AllowPreorder,
            InStock: stock.GetValueOrDefault(sku.Id) > 0)).ToList(),
        edition.LegalDocuments.Select(document => new LegalDocumentResponse(
            document.Id,
            document.DocNumber,
            document.DocType,
            document.Title,
            document.IssuedDate,
            document.EffectiveDate)).ToList());

    // Đầu sách hiển thị trên website: active và ấn bản mới nhất đã công bố (không còn nháp).
    private IQueryable<Book> PublishedBooks() => dbContext.Books.AsNoTracking()
        .Where(book => book.Status == BookStatus.Active
            && book.Editions.Any(edition => edition.IsLatest && edition.Status != EditionStatus.Draft));

    private async Task<IQueryable<Book>> ApplyFiltersAsync(
        IQueryable<Book> books,
        BooksQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Tạm dùng ILIKE; tìm không dấu và theo tác giả/ISBN sẽ chuyển sang Meilisearch (FR-11).
            var pattern = $"%{EscapeLike(query.Q.Trim())}%";
            books = books.Where(book => EF.Functions.ILike(book.Title, pattern, @"\")
                || book.Editions.Any(edition => edition.Isbn == query.Q.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categoryIds = await GetCategoryWithDescendantIdsAsync(query.Category, cancellationToken);
            books = books.Where(book => book.Categories.Any(category => categoryIds.Contains(category.Id)));
        }

        if (SnakeCaseEnumConverter<BookType>.TryParse(query.Type, out var bookType))
        {
            books = books.Where(book => book.BookType == bookType);
        }

        if (SnakeCaseEnumConverter<TargetAudience>.TryParse(query.Audience, out var audience))
        {
            books = books.Where(book => book.TargetAudiences.Contains(audience));
        }

        if (!string.IsNullOrWhiteSpace(query.LegalDocument))
        {
            var number = query.LegalDocument.Trim();
            var pattern = $"%{EscapeLike(number)}%";
            books = books.Where(book => book.Editions.Any(edition =>
                edition.LegalDocuments.Any(document => document.DocNumber == number)
                || EF.Functions.ILike(edition.UpdatedToText ?? string.Empty, pattern, @"\")));
        }

        var hasCover = SnakeCaseEnumConverter<CoverType>.TryParse(query.Cover, out var cover);
        if (hasCover || query.MinPrice is not null || query.MaxPrice is not null)
        {
            books = books.Where(book => book.Editions.Any(edition => edition.IsLatest && edition.Skus.Any(sku =>
                (!hasCover || sku.CoverType == cover)
                && (query.MinPrice == null || sku.SalePrice >= query.MinPrice)
                && (query.MaxPrice == null || sku.SalePrice <= query.MaxPrice))));
        }

        return books;
    }

    private static IQueryable<Book> Sort(IQueryable<Book> books, string sort) => sort switch
    {
        BookSorts.PriceAsc => books.OrderBy(book => book.Editions.Where(edition => edition.IsLatest)
            .SelectMany(edition => edition.Skus).Min(sku => (decimal?)sku.SalePrice)).ThenBy(book => book.Id),
        BookSorts.PriceDesc => books.OrderByDescending(book => book.Editions.Where(edition => edition.IsLatest)
            .SelectMany(edition => edition.Skus).Min(sku => (decimal?)sku.SalePrice)).ThenBy(book => book.Id),
        _ => books.OrderByDescending(book => book.Editions.Where(edition => edition.IsLatest)
            .Max(edition => edition.ReleaseDate)).ThenByDescending(book => book.Id),
    };

    private async Task<List<long>> GetCategoryWithDescendantIdsAsync(string slug, CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking()
            .Select(category => new { category.Id, category.Slug, category.ParentId })
            .ToListAsync(cancellationToken);
        var root = categories.FirstOrDefault(category => category.Slug == slug);
        if (root is null)
        {
            return [];
        }

        var byParent = categories.ToLookup(category => category.ParentId);
        var result = new List<long>();
        var pending = new Queue<long>([root.Id]);
        while (pending.TryDequeue(out var id))
        {
            result.Add(id);
            foreach (var child in byParent[id])
            {
                pending.Enqueue(child.Id);
            }
        }

        return result;
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}

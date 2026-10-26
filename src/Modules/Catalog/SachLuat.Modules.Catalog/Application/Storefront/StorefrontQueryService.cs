using System.Linq.Expressions;
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
internal sealed class StorefrontQueryService(
    CatalogDbContext dbContext,
    IInventoryService inventoryService,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<BookSummaryResponse>> ListBooksAsync(
        BooksQuery query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var books = PublishedBooks();
        books = await ApplyFiltersAsync(books, query, now, cancellationToken);

        var total = await books.CountAsync(cancellationToken);
        var page = await Sort(books, query.Sort, now)
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
                        Skus = edition.Skus.Select(sku => new
                        {
                            sku.Id,
                            sku.SkuCode,
                            sku.ListPrice,
                            sku.SalePrice,
                            Schedule = sku.PriceSchedules
                                .Where(schedule => schedule.StartsAt <= now && schedule.EndsAt > now
                                    && schedule.SalePrice < sku.SalePrice)
                                .Select(schedule => new { schedule.SalePrice, schedule.EndsAt })
                                .FirstOrDefault(),
                        }).ToList(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var skuIds = page.SelectMany(book => book.Latest?.Skus.Select(sku => sku.Id) ?? []).ToList();
        var stock = await inventoryService.GetAvailableQuantitiesAsync(skuIds, cancellationToken);

        var items = page.Select(book =>
        {
            var cheapest = book.Latest?.Skus
                .Select(sku => new
                {
                    sku.SkuCode,
                    sku.ListPrice,
                    Price = sku.Schedule?.SalePrice ?? sku.SalePrice,
                    SaleEndsAt = sku.Schedule?.EndsAt,
                })
                .MinBy(sku => sku.Price);
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
                cheapest?.SkuCode,
                cheapest?.Price,
                cheapest?.ListPrice,
                cheapest is null ? null : Pricing.DiscountPercent(cheapest.ListPrice, cheapest.Price),
                cheapest?.SaleEndsAt,
                InStock: book.Latest?.Skus.Any(sku => stock.GetValueOrDefault(sku.Id) > 0) ?? false);
        }).ToList();

        return PagedResponse<BookSummaryResponse>.Create(items, query, total);
    }

    public async Task<BookDetailResponse> GetBookAsync(string slug, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var book = await PublishedBooks()
            .Include(b => b.Categories)
            .Include(b => b.Authors)
            .Include(b => b.Editions.Where(edition => edition.Status != EditionStatus.Draft))
                .ThenInclude(edition => edition.Skus)
                .ThenInclude(sku => sku.PriceSchedules.Where(schedule => schedule.EndsAt > now))
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
            .Select(edition => ToEditionResponse(edition, stock, now))
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

    /// <remarks>Cần nạp lịch giá chưa kết thúc của các SKU để tính giá hiện hành.</remarks>
    internal static EditionResponse ToEditionResponse(
        Edition edition,
        IReadOnlyDictionary<long, int> stock,
        DateTime now) => new(
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
        edition.Skus.OrderBy(sku => sku.CoverType).Select(sku => ToSkuResponse(sku, stock, now)).ToList(),
        edition.LegalDocuments.Select(document => new LegalDocumentResponse(
            document.Id,
            document.DocNumber,
            document.DocType,
            document.Title,
            document.IssuedDate,
            document.EffectiveDate)).ToList());

    private static SkuResponse ToSkuResponse(Sku sku, IReadOnlyDictionary<long, int> stock, DateTime now)
    {
        var schedule = sku.ActivePriceScheduleAt(now);
        var price = schedule?.SalePrice ?? sku.SalePrice;
        return new SkuResponse(
            sku.Id,
            sku.SkuCode,
            sku.CoverType,
            sku.ListPrice,
            price,
            sku.SalePrice,
            Pricing.DiscountPercent(sku.ListPrice, price),
            schedule?.EndsAt,
            sku.AllowPreorder,
            InStock: stock.GetValueOrDefault(sku.Id) > 0);
    }

    // Đầu sách hiển thị trên website: active và ấn bản mới nhất đã công bố (không còn nháp).
    private IQueryable<Book> PublishedBooks() => dbContext.Books.AsNoTracking()
        .Where(book => book.Status == BookStatus.Active
            && book.Editions.Any(edition => edition.IsLatest && edition.Status != EditionStatus.Draft));

    private async Task<IQueryable<Book>> ApplyFiltersAsync(
        IQueryable<Book> books,
        BooksQuery query,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Tạm dùng ILIKE; tìm không dấu và theo tác giả/ISBN sẽ chuyển sang Meilisearch (FR-11).
            var term = query.Q.Trim();
            var pattern = LikePatterns.Contains(term);
            books = books.Where(book => EF.Functions.ILike(book.Title, pattern, LikePatterns.EscapeCharacter)
                || book.Editions.Any(edition => edition.Isbn == term));
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
            var pattern = LikePatterns.Contains(number);
            books = books.Where(book => book.Editions.Any(edition =>
                edition.LegalDocuments.Any(document => document.DocNumber == number)
                || EF.Functions.ILike(edition.UpdatedToText ?? string.Empty, pattern, LikePatterns.EscapeCharacter)));
        }

        // Lọc và sắp xếp theo giá hiện hành (đã áp lịch giá), khớp Sku.CurrentPriceAt.
        var hasCover = SnakeCaseEnumConverter<CoverType>.TryParse(query.Cover, out var cover);
        if (hasCover || query.MinPrice is not null || query.MaxPrice is not null)
        {
            books = books.Where(book => book.Editions.Any(edition => edition.IsLatest && edition.Skus
                .Where(sku => !hasCover || sku.CoverType == cover)
                .Select(sku => sku.PriceSchedules
                    .Where(schedule => schedule.StartsAt <= now && schedule.EndsAt > now
                        && schedule.SalePrice < sku.SalePrice)
                    .Select(schedule => (decimal?)schedule.SalePrice)
                    .FirstOrDefault() ?? sku.SalePrice)
                .Any(price => (query.MinPrice == null || price >= query.MinPrice)
                    && (query.MaxPrice == null || price <= query.MaxPrice))));
        }

        if (query.OnSale)
        {
            books = books.Where(book => book.Editions.Any(edition => edition.IsLatest && edition.Skus.Any(sku =>
                sku.SalePrice < sku.ListPrice
                || sku.PriceSchedules.Any(schedule => schedule.StartsAt <= now && schedule.EndsAt > now
                    && schedule.SalePrice < sku.SalePrice))));
        }

        return books;
    }

    private static IQueryable<Book> Sort(IQueryable<Book> books, string sort, DateTime now)
    {
        // Giá rẻ nhất của ấn bản mới nhất, theo giá hiện hành.
        Expression<Func<Book, decimal?>> cheapestPrice = book => book.Editions
            .Where(edition => edition.IsLatest)
            .SelectMany(edition => edition.Skus)
            .Min(sku => sku.PriceSchedules
                .Where(schedule => schedule.StartsAt <= now && schedule.EndsAt > now
                    && schedule.SalePrice < sku.SalePrice)
                .Select(schedule => (decimal?)schedule.SalePrice)
                .FirstOrDefault() ?? sku.SalePrice);

        return sort switch
        {
            BookSorts.PriceAsc => books.OrderBy(cheapestPrice).ThenBy(book => book.Id),
            BookSorts.PriceDesc => books.OrderByDescending(cheapestPrice).ThenBy(book => book.Id),
            _ => books.OrderByDescending(book => book.Editions.Where(edition => edition.IsLatest)
                .Max(edition => edition.ReleaseDate)).ThenByDescending(book => book.Id),
        };
    }

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
}

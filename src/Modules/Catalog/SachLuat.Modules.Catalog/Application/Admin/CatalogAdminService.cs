using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application.Admin;

/// <summary>Quản lý đầu sách, ấn bản, SKU trên Admin (FR-61, FR-62, FR-63).</summary>
internal sealed class CatalogAdminService(
    CatalogDbContext dbContext,
    IInventoryService inventoryService,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<AdminBookSummaryResponse>> ListBooksAsync(
        AdminBooksQuery query,
        CancellationToken cancellationToken)
    {
        var books = dbContext.Books.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var pattern = LikePatterns.Contains(term);
            var skuCode = term.ToUpperInvariant();
            books = books.Where(book => EF.Functions.ILike(book.Title, pattern, LikePatterns.EscapeCharacter)
                || book.Slug == term
                || book.Editions.Any(edition => edition.Isbn == term
                    || edition.Skus.Any(sku => sku.SkuCode == skuCode)));
        }

        if (SnakeCaseEnumConverter<BookStatus>.TryParse(query.Status, out var status))
        {
            books = books.Where(book => book.Status == status);
        }

        var total = await books.CountAsync(cancellationToken);
        var items = await books
            .OrderByDescending(book => book.UpdatedAt)
            .ThenByDescending(book => book.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(book => new AdminBookSummaryResponse(
                book.Id, book.Title, book.Slug, book.BookType, book.Status, book.Editions.Count, book.UpdatedAt))
            .ToListAsync(cancellationToken);

        return PagedResponse<AdminBookSummaryResponse>.Create(items, query, total);
    }

    public async Task<AdminBookResponse> GetBookAsync(long bookId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var book = await dbContext.Books.AsNoTracking()
            .Include(b => b.Categories)
            .Include(b => b.Authors)
            .Include(b => b.Editions).ThenInclude(edition => edition.Skus)
                .ThenInclude(sku => sku.PriceSchedules.Where(schedule => schedule.EndsAt > now))
            .Include(b => b.Editions).ThenInclude(edition => edition.LegalDocuments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken)
            ?? throw CatalogErrors.NotFound("đầu sách");

        var skuIds = book.Editions.SelectMany(edition => edition.Skus).Select(sku => sku.Id).ToList();
        var stock = await inventoryService.GetAvailableQuantitiesAsync(skuIds, cancellationToken);

        return new AdminBookResponse(
            book.Id,
            book.Title,
            book.Slug,
            book.Description,
            book.BookType,
            book.Status,
            book.TargetAudiences,
            book.MetaTitle,
            book.MetaDescription,
            book.Categories.Select(c => new NamedReference(c.Id, c.Name, c.Slug)).ToList(),
            book.Authors.Select(a => new NamedReference(a.Id, a.Name, a.Slug)).ToList(),
            book.Editions
                .OrderByDescending(edition => edition.IsLatest)
                .ThenByDescending(edition => edition.Id)
                .Select(edition => StorefrontQueryService.ToEditionResponse(edition, stock, now))
                .ToList(),
            book.CreatedAt,
            book.UpdatedAt);
    }

    public async Task<AdminBookResponse> CreateBookAsync(UpsertBookRequest request, CancellationToken cancellationToken)
    {
        var book = new Book(request.Title, request.Slug, request.BookType);
        await ApplyAsync(book, request, cancellationToken);
        dbContext.Books.Add(book);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return await GetBookAsync(book.Id, cancellationToken);
    }

    public async Task<AdminBookResponse> UpdateBookAsync(
        long bookId,
        UpsertBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .Include(b => b.Categories)
            .Include(b => b.Authors)
            .SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken)
            ?? throw CatalogErrors.NotFound("đầu sách");

        book.Rename(request.Title, request.Slug);
        await ApplyAsync(book, request, cancellationToken);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return await GetBookAsync(book.Id, cancellationToken);
    }

    /// <summary>Thêm ấn bản; ấn bản đầu tiên của đầu sách tự thành ấn bản mới nhất.</summary>
    public async Task<AdminBookResponse> AddEditionAsync(
        long bookId,
        UpsertEditionRequest request,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .Include(b => b.Editions)
            .SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken)
            ?? throw CatalogErrors.NotFound("đầu sách");

        var edition = book.AddEdition(ToPublishingInfo(request));
        await ApplyAsync(edition, request, cancellationToken);
        if (book.LatestEdition is null)
        {
            book.SetLatestEdition(edition);
        }

        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return await GetBookAsync(book.Id, cancellationToken);
    }

    public async Task<AdminBookResponse> UpdateEditionAsync(
        long editionId,
        UpsertEditionRequest request,
        CancellationToken cancellationToken)
    {
        var edition = await dbContext.Editions
            .Include(e => e.LegalDocuments)
            .SingleOrDefaultAsync(e => e.Id == editionId, cancellationToken)
            ?? throw CatalogErrors.NotFound("ấn bản");

        edition.UpdatePublishingInfo(ToPublishingInfo(request));
        await ApplyAsync(edition, request, cancellationToken);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return await GetBookAsync(edition.BookId, cancellationToken);
    }

    /// <summary>BR-02: đổi ấn bản mới nhất của đầu sách.</summary>
    public async Task<AdminBookResponse> SetLatestEditionAsync(long editionId, CancellationToken cancellationToken)
    {
        var bookId = await dbContext.Editions
            .Where(edition => edition.Id == editionId)
            .Select(edition => (long?)edition.BookId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw CatalogErrors.NotFound("ấn bản");
        var book = await dbContext.Books
            .Include(b => b.Editions)
            .SingleAsync(b => b.Id == bookId, cancellationToken);
        var edition = book.Editions.Single(e => e.Id == editionId);

        // Unique index ux_editions_book_id_latest kiểm tra ngay từng câu UPDATE (không deferrable),
        // nên phải bỏ cờ ấn bản cũ trước rồi mới đặt ấn bản mới, trong cùng một transaction.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (book.LatestEdition is { } current && !ReferenceEquals(current, edition))
        {
            current.SetLatest(false);
            await dbContext.SaveCatalogChangesAsync(cancellationToken);
        }

        book.SetLatestEdition(edition);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetBookAsync(bookId, cancellationToken);
    }

    public async Task<AdminBookResponse> AddSkuAsync(
        long editionId,
        CreateSkuRequest request,
        CancellationToken cancellationToken)
    {
        var edition = await dbContext.Editions
            .Include(e => e.Skus)
            .SingleOrDefaultAsync(e => e.Id == editionId, cancellationToken)
            ?? throw CatalogErrors.NotFound("ấn bản");

        var sku = edition.AddSku(request.SkuCode, request.CoverType, request.ListPrice, request.SalePrice);
        sku.SetPreorder(request.AllowPreorder);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return await GetBookAsync(edition.BookId, cancellationToken);
    }

    public async Task<AdminBookResponse> UpdateSkuAsync(
        long skuId,
        UpdateSkuRequest request,
        CancellationToken cancellationToken)
    {
        var sku = await dbContext.Skus.SingleOrDefaultAsync(s => s.Id == skuId, cancellationToken)
            ?? throw CatalogErrors.NotFound("SKU");

        sku.ChangePrices(request.ListPrice, request.SalePrice);
        sku.SetPreorder(request.AllowPreorder);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);

        var bookId = await dbContext.Editions
            .Where(edition => edition.Id == sku.EditionId)
            .Select(edition => edition.BookId)
            .SingleAsync(cancellationToken);
        return await GetBookAsync(bookId, cancellationToken);
    }

    private async Task ApplyAsync(Book book, UpsertBookRequest request, CancellationToken cancellationToken)
    {
        book.UpdateDetails(
            request.BookType,
            request.Description,
            request.TargetAudiences ?? [],
            request.MetaTitle,
            request.MetaDescription);
        book.ChangeStatus(request.Status);

        var categoryIds = (request.CategoryIds ?? []).Distinct().ToList();
        var categories = await dbContext.Categories
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
        if (categories.Count != categoryIds.Count)
        {
            throw CatalogErrors.ReferenceNotFound("danh mục");
        }

        var authorIds = (request.AuthorIds ?? []).Distinct().ToList();
        var authors = await dbContext.Authors
            .Where(author => authorIds.Contains(author.Id))
            .ToListAsync(cancellationToken);
        if (authors.Count != authorIds.Count)
        {
            throw CatalogErrors.ReferenceNotFound("tác giả");
        }

        book.AssignCategories(categories);
        book.AssignAuthors(authors);
    }

    private async Task ApplyAsync(Edition edition, UpsertEditionRequest request, CancellationToken cancellationToken)
    {
        edition.UpdateDetails(new EditionDetails(
            request.UpdatedToText,
            request.UpdatedToDate,
            request.Pages,
            request.Size,
            request.WeightGram,
            request.CoverImageUrl,
            request.PreviewFileUrl,
            request.TableOfContents,
            request.ReleaseDate));
        edition.ChangeStatus(request.Status);

        var documentIds = (request.LegalDocumentIds ?? []).Distinct().ToList();
        var documents = await dbContext.LegalDocuments
            .Where(document => documentIds.Contains(document.Id))
            .ToListAsync(cancellationToken);
        if (documents.Count != documentIds.Count)
        {
            throw CatalogErrors.ReferenceNotFound("văn bản pháp luật");
        }

        edition.SetLegalDocuments(documents);
    }

    private static EditionPublishingInfo ToPublishingInfo(UpsertEditionRequest request) => new(
        request.Isbn,
        request.Publisher,
        request.PublishingDecisionNo,
        request.RegistrationNo,
        request.EditionName,
        request.PublishYear);
}

using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Authors;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.Modules.Catalog.Domain.LegalDocuments;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application.Admin;

/// <summary>Danh mục, tác giả (FR-64) và văn bản pháp luật. Xóa là xóa mềm.</summary>
internal sealed class ReferenceDataAdminService(CatalogDbContext dbContext)
{
    public async Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryResponse(
                category.Id, category.Name, category.Slug, category.ParentId, category.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<CategoryResponse> CreateCategoryAsync(
        UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var all = await LoadAllCategoriesAsync(cancellationToken);
        var category = new Category(request.Name, request.Slug, FindParent(all, request.ParentId));
        category.SetSortOrder(request.SortOrder);
        dbContext.Categories.Add(category);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(
        long categoryId,
        UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var all = await LoadAllCategoriesAsync(cancellationToken);
        var category = all.SingleOrDefault(c => c.Id == categoryId) ?? throw CatalogErrors.NotFound("danh mục");
        category.Rename(request.Name, request.Slug);
        category.MoveTo(FindParent(all, request.ParentId));
        category.SetSortOrder(request.SortOrder);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    public async Task DeleteCategoryAsync(long categoryId, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw CatalogErrors.NotFound("danh mục");
        if (await dbContext.Categories.AnyAsync(c => c.ParentId == categoryId, cancellationToken))
        {
            throw CatalogErrors.CategoryHasChildren();
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<AuthorResponse>> ListAuthorsAsync(
        AdminSearchQuery query,
        CancellationToken cancellationToken)
    {
        var authors = dbContext.Authors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = LikePatterns.Contains(query.Q.Trim());
            authors = authors.Where(author => EF.Functions.ILike(author.Name, pattern, LikePatterns.EscapeCharacter));
        }

        var total = await authors.CountAsync(cancellationToken);
        var items = await authors
            .OrderBy(author => author.SortOrder)
            .ThenBy(author => author.Name)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(author => new AuthorResponse(
                author.Id, author.Name, author.Slug, author.Biography, author.SortOrder))
            .ToListAsync(cancellationToken);
        return PagedResponse<AuthorResponse>.Create(items, query, total);
    }

    public async Task<AuthorResponse> CreateAuthorAsync(UpsertAuthorRequest request, CancellationToken cancellationToken)
    {
        var author = new Author(request.Name, request.Slug);
        Apply(author, request);
        dbContext.Authors.Add(author);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(author);
    }

    public async Task<AuthorResponse> UpdateAuthorAsync(
        long authorId,
        UpsertAuthorRequest request,
        CancellationToken cancellationToken)
    {
        var author = await dbContext.Authors.SingleOrDefaultAsync(a => a.Id == authorId, cancellationToken)
            ?? throw CatalogErrors.NotFound("tác giả");
        author.Rename(request.Name, request.Slug);
        Apply(author, request);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(author);
    }

    public async Task DeleteAuthorAsync(long authorId, CancellationToken cancellationToken)
    {
        var author = await dbContext.Authors.SingleOrDefaultAsync(a => a.Id == authorId, cancellationToken)
            ?? throw CatalogErrors.NotFound("tác giả");
        dbContext.Authors.Remove(author);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<LegalDocumentResponse>> ListLegalDocumentsAsync(
        AdminSearchQuery query,
        CancellationToken cancellationToken)
    {
        var documents = dbContext.LegalDocuments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = LikePatterns.Contains(query.Q.Trim());
            documents = documents.Where(document =>
                EF.Functions.ILike(document.DocNumber, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(document.Title, pattern, LikePatterns.EscapeCharacter));
        }

        var total = await documents.CountAsync(cancellationToken);
        var items = await documents
            .OrderByDescending(document => document.IssuedDate)
            .ThenByDescending(document => document.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(document => new LegalDocumentResponse(
                document.Id,
                document.DocNumber,
                document.DocType,
                document.Title,
                document.IssuedDate,
                document.EffectiveDate))
            .ToListAsync(cancellationToken);
        return PagedResponse<LegalDocumentResponse>.Create(items, query, total);
    }

    public async Task<LegalDocumentResponse> CreateLegalDocumentAsync(
        UpsertLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var document = new LegalDocument(request.DocNumber, request.DocType, request.Title);
        document.Update(request.DocNumber, request.DocType, request.Title, request.IssuedDate, request.EffectiveDate);
        dbContext.LegalDocuments.Add(document);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(document);
    }

    public async Task<LegalDocumentResponse> UpdateLegalDocumentAsync(
        long documentId,
        UpsertLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.LegalDocuments.SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw CatalogErrors.NotFound("văn bản pháp luật");
        document.Update(request.DocNumber, request.DocType, request.Title, request.IssuedDate, request.EffectiveDate);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        return ToResponse(document);
    }

    public async Task DeleteLegalDocumentAsync(long documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.LegalDocuments.SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw CatalogErrors.NotFound("văn bản pháp luật");
        dbContext.LegalDocuments.Remove(document);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
    }

    // Nạp cả cây (danh mục ít) để Category.MoveTo kiểm tra vòng lặp qua chuỗi Parent.
    private Task<List<Category>> LoadAllCategoriesAsync(CancellationToken cancellationToken) =>
        dbContext.Categories.ToListAsync(cancellationToken);

    private static Category? FindParent(List<Category> all, long? parentId) => parentId is null
        ? null
        : all.SingleOrDefault(c => c.Id == parentId) ?? throw CatalogErrors.ReferenceNotFound("danh mục cha");

    private static void Apply(Author author, UpsertAuthorRequest request)
    {
        author.UpdateBiography(request.Biography);
        author.SetSortOrder(request.SortOrder);
    }

    private static CategoryResponse ToResponse(Category category) =>
        new(category.Id, category.Name, category.Slug, category.ParentId, category.SortOrder);

    private static AuthorResponse ToResponse(Author author) =>
        new(author.Id, author.Name, author.Slug, author.Biography, author.SortOrder);

    private static LegalDocumentResponse ToResponse(LegalDocument document) => new(
        document.Id, document.DocNumber, document.DocType, document.Title, document.IssuedDate, document.EffectiveDate);
}

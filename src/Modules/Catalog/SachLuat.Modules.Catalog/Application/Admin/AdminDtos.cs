using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Catalog.Application.Admin;

internal sealed class AdminBooksQuery : PagedQuery
{
    public string? Q { get; set; }

    /// <summary><c>draft</c>, <c>active</c>, <c>hidden</c>.</summary>
    public string? Status { get; set; }
}

/// <summary>Tìm theo tên (tác giả, danh mục) hoặc số hiệu/trích yếu (văn bản pháp luật).</summary>
internal sealed class AdminSearchQuery : PagedQuery
{
    public string? Q { get; set; }
}

internal sealed record UpsertBookRequest(
    string Title,
    string Slug,
    BookType BookType,
    BookStatus Status,
    string? Description,
    IReadOnlyList<TargetAudience>? TargetAudiences,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<long>? CategoryIds,
    IReadOnlyList<long>? AuthorIds);

internal sealed record UpsertEditionRequest(
    string Isbn,
    string Publisher,
    string PublishingDecisionNo,
    EditionStatus Status,
    string? RegistrationNo,
    string? EditionName,
    short? PublishYear,
    string? UpdatedToText,
    DateOnly? UpdatedToDate,
    int? Pages,
    string? Size,
    int? WeightGram,
    string? CoverImageUrl,
    string? PreviewFileUrl,
    string? TableOfContents,
    DateOnly? ReleaseDate,
    IReadOnlyList<long>? LegalDocumentIds);

internal sealed record CreateSkuRequest(
    string SkuCode,
    CoverType CoverType,
    decimal ListPrice,
    decimal SalePrice,
    bool AllowPreorder);

internal sealed record UpdateSkuRequest(decimal ListPrice, decimal SalePrice, bool AllowPreorder);

/// <param name="StartsAt">Có offset (ví dụ <c>2026-11-01T00:00:00+07:00</c>); lưu UTC.</param>
/// <param name="EndsAt">Hết giảm giá từ thời điểm này.</param>
internal sealed record UpsertPriceScheduleRequest(
    decimal SalePrice,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Note);

/// <param name="DiscountPercent">% giảm so với giá bìa.</param>
internal sealed record PriceScheduleResponse(
    long Id,
    long SkuId,
    decimal SalePrice,
    int DiscountPercent,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Note,
    PriceScheduleStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed record UpsertCategoryRequest(string Name, string Slug, long? ParentId, int SortOrder);

internal sealed record UpsertAuthorRequest(string Name, string Slug, string? Biography, int SortOrder);

internal sealed record UpsertLegalDocumentRequest(
    string DocNumber,
    LegalDocumentType DocType,
    string Title,
    DateOnly? IssuedDate,
    DateOnly? EffectiveDate);

internal sealed record AdminBookSummaryResponse(
    long Id,
    string Title,
    string Slug,
    BookType BookType,
    BookStatus Status,
    int EditionCount,
    DateTime UpdatedAt);

internal sealed record AdminBookResponse(
    long Id,
    string Title,
    string Slug,
    string? Description,
    BookType BookType,
    BookStatus Status,
    IReadOnlyList<TargetAudience> TargetAudiences,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<NamedReference> Categories,
    IReadOnlyList<NamedReference> Authors,
    IReadOnlyList<EditionResponse> Editions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed record CategoryResponse(long Id, string Name, string Slug, long? ParentId, int SortOrder);

internal sealed record AuthorResponse(long Id, string Name, string Slug, string? Biography, int SortOrder);

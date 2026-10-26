using SachLuat.Modules.Catalog.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Catalog.Application.Storefront;

/// <summary>
/// Lọc danh sách sách (FR-12, FR-13, FR-14). Enum nhận giá trị snake_case: <c>type=code</c>,
/// <c>audience=lawyer</c>, <c>cover=paperback</c>, <c>sort=price_asc</c>.
/// </summary>
internal sealed class BooksQuery : PagedQuery
{
    public string? Q { get; set; }

    /// <summary>Slug danh mục, gồm cả danh mục con.</summary>
    public string? Category { get; set; }

    public string? Type { get; set; }

    public string? Audience { get; set; }

    public string? Cover { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    /// <summary>Số hiệu văn bản, ví dụ <c>91/2015/QH13</c> (FR-14).</summary>
    public string? LegalDocument { get; set; }

    /// <summary><c>true</c>: chỉ sách có SKU đang bán thấp hơn giá bìa (FR-12).</summary>
    public bool OnSale { get; set; }

    public string Sort { get; set; } = BookSorts.Newest;
}

internal static class BookSorts
{
    public const string Newest = "newest";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Newest, PriceAsc, PriceDesc };
}

internal sealed record BookSummaryResponse(
    long Id,
    string Title,
    string Slug,
    BookType BookType,
    string? EditionName,
    short? PublishYear,
    string? CoverImageUrl,
    string? UpdatedToText,
    EditionStatus? EditionStatus,
    string? SkuCode,
    decimal? SalePrice,
    decimal? ListPrice,
    int? DiscountPercent,
    DateTime? SaleEndsAt,
    bool InStock);

internal sealed record NamedReference(long Id, string Name, string Slug);

internal sealed record LegalDocumentResponse(
    long Id,
    string DocNumber,
    LegalDocumentType DocType,
    string Title,
    DateOnly? IssuedDate,
    DateOnly? EffectiveDate);

/// <param name="SalePrice">Giá khách trả lúc này (đã áp lịch giá).</param>
/// <param name="RegularSalePrice">Giá bán thường, khi không có lịch giá.</param>
/// <param name="SaleEndsAt">Hết giá lịch lúc này (UTC); null khi đang bán giá thường.</param>
internal sealed record SkuResponse(
    long Id,
    string SkuCode,
    CoverType CoverType,
    decimal ListPrice,
    decimal SalePrice,
    decimal RegularSalePrice,
    int DiscountPercent,
    DateTime? SaleEndsAt,
    bool AllowPreorder,
    bool InStock);

internal sealed record EditionResponse(
    long Id,
    string? EditionName,
    string Isbn,
    string Publisher,
    string PublishingDecisionNo,
    string? RegistrationNo,
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
    bool IsLatest,
    EditionStatus Status,
    IReadOnlyList<SkuResponse> Skus,
    IReadOnlyList<LegalDocumentResponse> LegalDocuments);

/// <summary>Chi tiết sách; ấn bản mới nhất đứng đầu, ấn bản cũ có <c>isLatest = false</c> để hiện cảnh báo BR-03.</summary>
internal sealed record BookDetailResponse(
    long Id,
    string Title,
    string Slug,
    string? Description,
    BookType BookType,
    IReadOnlyList<TargetAudience> TargetAudiences,
    string? MetaTitle,
    string? MetaDescription,
    IReadOnlyList<NamedReference> Categories,
    IReadOnlyList<NamedReference> Authors,
    IReadOnlyList<EditionResponse> Editions);

internal sealed record CategoryNode(long Id, string Name, string Slug, IReadOnlyList<CategoryNode> Children);

internal static class Pricing
{
    /// <summary>% giảm so với giá bìa, làm tròn xuống (không hiển thị "giảm 0%" do làm tròn lên).</summary>
    public static int DiscountPercent(decimal listPrice, decimal salePrice) =>
        listPrice <= 0 ? 0 : (int)decimal.Floor((listPrice - salePrice) * 100 / listPrice);
}

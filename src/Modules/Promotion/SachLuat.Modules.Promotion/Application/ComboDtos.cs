using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Promotion.Application;

/// <param name="SkuId">SKU của combo; mỗi SKU một dòng, muốn nhiều cuốn thì tăng <paramref name="Quantity"/>.</param>
/// <param name="Quantity">Số cuốn của SKU này trong một bộ.</param>
internal sealed record ComboItemRequest(long SkuId, int Quantity);

/// <param name="ComboPrice">Giá một bộ (VNĐ nguyên), phải thấp hơn tổng giá bán thường của các sách.</param>
/// <param name="StartsAt">Gửi kèm offset; lưu và trả về UTC.</param>
internal sealed record UpsertComboRequest(
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    decimal ComboPrice,
    IReadOnlyList<ComboItemRequest>? Items,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

/// <param name="SkuCode">null khi SKU không còn ở Catalog.</param>
/// <param name="RegularSalePrice">Giá bán thường (chưa áp lịch giá) của SKU.</param>
internal sealed record AdminComboItemResponse(
    long SkuId,
    int Quantity,
    string? SkuCode,
    string? BookTitle,
    decimal? RegularSalePrice);

/// <param name="RegularTotal">Tổng giá bán thường của một bộ; null khi có SKU không còn ở Catalog.</param>
internal sealed record AdminComboResponse(
    long Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    decimal ComboPrice,
    decimal? RegularTotal,
    IReadOnlyList<AdminComboItemResponse> Items,
    bool IsActive,
    DateTime StartsAt,
    DateTime? EndsAt,
    PromotionStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed class AdminCombosQuery : PagedQuery
{
    /// <summary>Tìm theo tên hoặc slug.</summary>
    public string? Q { get; set; }
}

/// <param name="UnitPrice">Giá hiện hành của SKU (đã áp lịch giá).</param>
internal sealed record ComboItemResponse(
    long SkuId,
    string SkuCode,
    string BookTitle,
    string BookSlug,
    string? EditionName,
    string CoverType,
    string? CoverImageUrl,
    int Quantity,
    decimal UnitPrice,
    bool IsSellable);

/// <param name="RetailPrice">Tổng giá hiện hành của một bộ nếu mua lẻ.</param>
/// <param name="Savings">Số tiền tiết kiệm một bộ so với mua lẻ, không âm.</param>
/// <param name="IsSellable">false khi có sách trong bộ tạm ngừng bán; FE nên tắt nút mua combo.</param>
internal sealed record ComboResponse(
    long Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    decimal ComboPrice,
    decimal RetailPrice,
    decimal Savings,
    IReadOnlyList<ComboItemResponse> Items,
    bool IsSellable,
    DateTime StartsAt,
    DateTime? EndsAt);

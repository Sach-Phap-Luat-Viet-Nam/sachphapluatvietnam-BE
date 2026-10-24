using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Promotion.Application;

/// <param name="TargetIds">Id sách, SKU hoặc danh mục theo <paramref name="ScopeType"/>; bỏ trống khi phạm vi là <c>all</c>.</param>
/// <param name="MaxDiscountAmount">Trần số tiền giảm, chỉ dùng khi giảm theo phần trăm.</param>
/// <param name="StartsAt">Gửi kèm offset; lưu và trả về UTC.</param>
internal sealed record UpsertPromotionRequest(
    string Name,
    string Title,
    string? Description,
    PromotionKind Kind,
    PromotionMode Mode,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    PromotionScopeType ScopeType,
    IReadOnlyList<long>? TargetIds,
    bool ExcludeOnSaleItems,
    PromotionAudience Audience,
    int? UsageLimit,
    int? PerCustomerLimit,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    int Priority,
    bool IsPublic);

internal sealed record AdminPromotionResponse(
    long Id,
    string Name,
    string Title,
    string? Description,
    PromotionKind Kind,
    PromotionMode Mode,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    PromotionScopeType ScopeType,
    IReadOnlyList<long> TargetIds,
    bool ExcludeOnSaleItems,
    PromotionAudience Audience,
    int? UsageLimit,
    int? PerCustomerLimit,
    int UsedCount,
    int CouponCount,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    bool IsPublic,
    PromotionStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Ưu đãi hiển thị cho khách ở trang khuyến mãi; không chứa mã giảm giá.</summary>
internal sealed record PublicPromotionResponse(
    long Id,
    string Title,
    string? Description,
    PromotionKind Kind,
    PromotionMode Mode,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    PromotionScopeType ScopeType,
    bool ExcludeOnSaleItems,
    PromotionAudience Audience,
    DateTime StartsAt,
    DateTime? EndsAt);

internal sealed class AdminPromotionsQuery : PagedQuery
{
    /// <summary>Tìm theo tên nội bộ hoặc tiêu đề.</summary>
    public string? Q { get; set; }
}

internal sealed record CreateCouponRequest(string Code, int? UsageLimit);

/// <param name="Prefix">Tiền tố mã, chữ/số/gạch nối, tối đa 12 ký tự.</param>
/// <param name="UsageLimit">Lượt dùng mỗi mã; bỏ trống là 1 (mã dùng một lần).</param>
internal sealed record GenerateCouponsRequest(int Count, string? Prefix, int? UsageLimit);

internal sealed record UpdateCouponRequest(bool IsActive, int? UsageLimit);

internal sealed record CouponResponse(
    long Id,
    long PromotionId,
    string Code,
    bool IsActive,
    int? UsageLimit,
    int UsedCount,
    DateTime CreatedAt);

internal sealed record PromotionStatsResponse(
    long PromotionId,
    int UsedCount,
    int ReleasedCount,
    decimal TotalOrderDiscount,
    decimal TotalShippingDiscount,
    int UniqueCustomers);

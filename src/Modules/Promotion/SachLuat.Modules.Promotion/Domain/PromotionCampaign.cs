using System.Linq.Expressions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Promotion.Domain;

/// <summary>
/// Chương trình khuyến mãi (FR-90): giảm trên tổng đơn hoặc giảm phí ship, tự áp hoặc dùng mã.
/// Ví dụ "Giảm 20% tổng hóa đơn đến hết 31/12/2026" là <c>OrderDiscount</c> + <c>Automatic</c> + <c>Percent 20</c>.
/// </summary>
internal sealed class PromotionCampaign : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 255;
    public const int DescriptionMaxLength = 1000;
    public const decimal MaxMoney = 1_000_000_000m;
    public const int MaxTargets = 500;

    private readonly List<PromotionTarget> _targets = [];

    public PromotionCampaign(PromotionDetails details, IEnumerable<long> targetIds)
    {
        Update(details);
        SetTargets(targetIds);
    }

    // EF Core
    private PromotionCampaign()
    {
    }

    /// <summary>Tên nội bộ cho nhân viên.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Tiêu đề hiển thị cho khách.</summary>
    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PromotionKind Kind { get; private set; }

    public PromotionMode Mode { get; private set; }

    public DiscountType DiscountType { get; private set; }

    /// <summary>Phần trăm (1–100) hoặc số tiền VNĐ, tùy <see cref="DiscountType"/>.</summary>
    public decimal DiscountValue { get; private set; }

    /// <summary>Trần số tiền giảm khi giảm theo phần trăm; null là không giới hạn.</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    /// <summary>Tạm tính tối thiểu của cả đơn (trước giảm giá) để được áp dụng.</summary>
    public decimal MinOrderAmount { get; private set; }

    public PromotionScopeType ScopeType { get; private set; }

    /// <summary>Sách đang chạy lịch giá (đã giảm sẵn) không được tính thêm khuyến mãi này.</summary>
    public bool ExcludeOnSaleItems { get; private set; }

    public PromotionAudience Audience { get; private set; }

    /// <summary>Tổng số lượt dùng; null là không giới hạn.</summary>
    public int? UsageLimit { get; private set; }

    /// <summary>Số lượt tối đa mỗi khách; null là không giới hạn.</summary>
    public int? PerCustomerLimit { get; private set; }

    public int UsedCount { get; private set; }

    public DateTime StartsAt { get; private set; }

    /// <summary>null: chạy đến khi tắt.</summary>
    public DateTime? EndsAt { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Hai chương trình cùng giảm như nhau thì chương trình ưu tiên cao hơn được chọn.</summary>
    public int Priority { get; private set; }

    /// <summary>Có hiện ở trang khuyến mãi công khai hay không (mã riêng gửi cho từng khách thì tắt).</summary>
    public bool IsPublic { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyCollection<PromotionTarget> Targets => _targets;

    /// <summary>Đang bật, trong lịch chạy và còn lượt dùng tại <paramref name="now"/> (UTC); dùng được trong truy vấn EF.</summary>
    public static Expression<Func<PromotionCampaign, bool>> RunningAt(DateTime now) =>
        promotion => promotion.IsActive
            && promotion.StartsAt <= now
            && (promotion.EndsAt == null || promotion.EndsAt > now)
            && (promotion.UsageLimit == null || promotion.UsedCount < promotion.UsageLimit);

    /// <summary>Cùng điều kiện với <see cref="RunningAt"/> nhưng cho một thực thể đã nạp.</summary>
    public bool IsRunningAt(DateTime now) =>
        IsActive && StartsAt <= now && (EndsAt == null || EndsAt > now) && (UsageLimit == null || UsedCount < UsageLimit);

    public void Update(PromotionDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name) || string.IsNullOrWhiteSpace(details.Title))
        {
            throw PromotionErrors.Invalid("Thiếu tên hoặc tiêu đề chương trình");
        }

        if (details.EndsAt is { } endsAt && endsAt <= details.StartsAt)
        {
            throw PromotionErrors.Invalid("Thời gian kết thúc phải sau thời gian bắt đầu");
        }

        // Combo có bảng và API riêng; kind này chỉ dùng để báo cáo ưu đãi đã áp vào đơn.
        if (details.Kind is not (PromotionKind.OrderDiscount or PromotionKind.FreeShipping))
        {
            throw PromotionErrors.Invalid("Loại chương trình không hợp lệ");
        }

        ValidateDiscount(details);

        if (details.MinOrderAmount is < 0 or > MaxMoney)
        {
            throw PromotionErrors.Invalid("Giá trị đơn tối thiểu không hợp lệ");
        }

        if (details.UsageLimit is <= 0 || details.PerCustomerLimit is <= 0)
        {
            throw PromotionErrors.Invalid("Giới hạn lượt dùng phải lớn hơn 0");
        }

        // Giảm phí ship áp lên phí ship của cả đơn nên phạm vi sách không có nghĩa.
        if (details.Kind == PromotionKind.FreeShipping
            && (details.ScopeType != PromotionScopeType.All || details.ExcludeOnSaleItems))
        {
            throw PromotionErrors.Invalid("Chương trình giảm phí ship áp dụng cho cả đơn, không chọn phạm vi sách");
        }

        Name = details.Name.Trim();
        Title = details.Title.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Kind = details.Kind;
        Mode = details.Mode;
        DiscountType = details.DiscountType;
        DiscountValue = details.DiscountValue;
        MaxDiscountAmount = details.DiscountType == DiscountType.Percent ? details.MaxDiscountAmount : null;
        MinOrderAmount = details.MinOrderAmount;
        ScopeType = details.ScopeType;
        ExcludeOnSaleItems = details.ExcludeOnSaleItems;
        Audience = details.Audience;
        UsageLimit = details.UsageLimit;
        PerCustomerLimit = details.PerCustomerLimit;
        StartsAt = details.StartsAt;
        EndsAt = details.EndsAt;
        IsActive = details.IsActive;
        Priority = details.Priority;
        IsPublic = details.IsPublic;
    }

    /// <summary>
    /// Thay danh sách đối tượng theo <see cref="ScopeType"/> (id sách, SKU hoặc danh mục). Phạm vi <c>All</c> không
    /// có đối tượng. Không kiểm tra id có tồn tại vì Catalog là module khác (không khóa ngoại chéo schema).
    /// </summary>
    public void SetTargets(IEnumerable<long> targetIds)
    {
        var ids = targetIds.Distinct().ToList();
        if (ScopeType == PromotionScopeType.All ? ids.Count > 0 : ids.Count is 0 or > MaxTargets)
        {
            throw PromotionErrors.Invalid(ScopeType == PromotionScopeType.All
                ? "Phạm vi \"tất cả sách\" không có danh sách đối tượng"
                : $"Phạm vi cần từ 1 đến {MaxTargets} đối tượng");
        }

        if (ids.Any(id => id <= 0))
        {
            throw PromotionErrors.Invalid("Mã đối tượng phạm vi không hợp lệ");
        }

        _targets.RemoveAll(target => !ids.Contains(target.TargetId));
        var existing = _targets.Select(target => target.TargetId).ToHashSet();
        _targets.AddRange(ids.Where(id => !existing.Contains(id)).Select(id => new PromotionTarget(id)));
    }

    public PromotionStatus StatusAt(DateTime now)
    {
        if (!IsActive)
        {
            return PromotionStatus.Inactive;
        }

        if (now < StartsAt)
        {
            return PromotionStatus.Scheduled;
        }

        if (EndsAt <= now)
        {
            return PromotionStatus.Ended;
        }

        return UsageLimit <= UsedCount ? PromotionStatus.Exhausted : PromotionStatus.Running;
    }

    /// <summary>Có sách nào trong dòng đơn thuộc phạm vi chương trình không (chưa xét sách đang có lịch giá).</summary>
    public bool CoversLine(PromotionLine line) => ScopeType switch
    {
        PromotionScopeType.All => true,
        PromotionScopeType.Books => _targets.Any(target => target.TargetId == line.BookId),
        PromotionScopeType.Skus => _targets.Any(target => target.TargetId == line.SkuId),
        PromotionScopeType.Categories => _targets.Any(target => line.CategoryIds.Contains(target.TargetId)),
        _ => false,
    };

    public void IncrementUsage() => UsedCount++;

    public void DecrementUsage() => UsedCount = Math.Max(0, UsedCount - 1);

    private static void ValidateDiscount(PromotionDetails details)
    {
        var valid = details.DiscountType switch
        {
            DiscountType.Percent => details.DiscountValue is > 0 and <= 100,
            DiscountType.FixedAmount => details.DiscountValue is > 0 and <= MaxMoney,
            _ => false,
        };
        if (!valid)
        {
            throw PromotionErrors.Invalid("Mức giảm không hợp lệ");
        }

        if (details.MaxDiscountAmount is <= 0 or > MaxMoney)
        {
            throw PromotionErrors.Invalid("Mức giảm tối đa không hợp lệ");
        }
    }
}

internal sealed class PromotionTarget(long targetId)
{
    public long Id { get; private set; }

    public long PromotionId { get; private set; }

    /// <summary>Id sách, SKU hoặc danh mục, tùy <see cref="PromotionCampaign.ScopeType"/>.</summary>
    public long TargetId { get; private set; } = targetId;
}

/// <param name="StartsAt">UTC.</param>
/// <param name="EndsAt">UTC.</param>
internal sealed record PromotionDetails(
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
    bool ExcludeOnSaleItems,
    PromotionAudience Audience,
    int? UsageLimit,
    int? PerCustomerLimit,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    bool IsPublic);

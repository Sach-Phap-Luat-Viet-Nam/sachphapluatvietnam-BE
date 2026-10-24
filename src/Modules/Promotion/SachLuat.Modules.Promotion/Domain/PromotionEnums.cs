namespace SachLuat.Modules.Promotion.Domain;

/// <summary>Cách khách nhận ưu đãi.</summary>
internal enum PromotionMode
{
    /// <summary>Tự áp khi đủ điều kiện, khách không cần nhập mã.</summary>
    Automatic,

    /// <summary>Khách nhập mã giảm giá.</summary>
    Coupon,
}

internal enum DiscountType
{
    /// <summary>Giá trị là phần trăm (1–100).</summary>
    Percent,

    /// <summary>Giá trị là số tiền VNĐ.</summary>
    FixedAmount,
}

/// <summary>Phạm vi sách được tính giảm giá (chỉ với chương trình giảm trên đơn).</summary>
internal enum PromotionScopeType
{
    All,
    Books,
    Skus,

    /// <summary>Gồm cả sách thuộc danh mục con.</summary>
    Categories,
}

internal enum PromotionAudience
{
    Everyone,
    MembersOnly,
}

/// <summary>Trạng thái tính theo thời điểm hiện tại, chỉ để hiển thị trên Admin (không lưu).</summary>
internal enum PromotionStatus
{
    Inactive,
    Scheduled,
    Running,
    Ended,
    Exhausted,
}

internal enum RedemptionStatus
{
    Redeemed,
    Released,
}

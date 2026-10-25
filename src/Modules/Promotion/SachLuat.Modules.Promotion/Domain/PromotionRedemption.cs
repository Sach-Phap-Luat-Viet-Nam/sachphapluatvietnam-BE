using SachLuat.Modules.Promotion.Contracts;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Promotion.Domain;

/// <summary>
/// Một lượt dùng chương trình cho một đơn. Duy nhất theo (<see cref="Reference"/>, <see cref="PromotionId"/>) nên
/// ghi nhận lại cùng đơn không tính trùng. Lượt bị hủy được giữ lại (đổi trạng thái) để đối soát.
/// </summary>
internal sealed class PromotionRedemption
{
    public const int ReferenceMaxLength = 64;
    public const int CustomerKeyMaxLength = 40;

    public PromotionRedemption(
        string reference,
        long promotionId,
        long? couponId,
        string? customerKey,
        PromotionKind kind,
        decimal amount,
        DateTime now)
    {
        Reference = reference;
        PromotionId = promotionId;
        CouponId = couponId;
        CustomerKey = customerKey;
        Kind = kind;
        Amount = amount;
        Status = RedemptionStatus.Redeemed;
        RedeemedAt = now;
    }

    // EF Core
    private PromotionRedemption()
    {
    }

    public long Id { get; private set; }

    /// <summary>Mã đơn.</summary>
    public string Reference { get; private set; } = string.Empty;

    public long PromotionId { get; private set; }

    public long? CouponId { get; private set; }

    /// <summary>Định danh khách để tính giới hạn mỗi khách; null khi không biết (xem <see cref="PromotionCustomerKey"/>).</summary>
    public string? CustomerKey { get; private set; }

    public PromotionKind Kind { get; private set; }

    /// <summary>Số tiền giảm trên đơn hoặc trên phí ship, tùy <see cref="Kind"/>.</summary>
    public decimal Amount { get; private set; }

    public RedemptionStatus Status { get; private set; }

    public DateTime RedeemedAt { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    public void Release(DateTime now)
    {
        Status = RedemptionStatus.Released;
        ReleasedAt = now;
    }
}

internal static class PromotionCustomerKey
{
    /// <summary>Thành viên theo tài khoản, khách vãng lai theo số điện thoại đã chuẩn hóa; không biết thì null.</summary>
    public static string? From(PromotionCustomer customer)
    {
        if (customer.UserId is { } userId)
        {
            return $"u:{userId}";
        }

        var phone = PhoneNumbers.Normalize(customer.Phone);
        return phone.Length == 0 ? null : $"p:{phone}";
    }
}

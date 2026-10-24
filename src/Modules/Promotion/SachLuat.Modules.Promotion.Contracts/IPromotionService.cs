namespace SachLuat.Modules.Promotion.Contracts;

/// <summary>
/// Khuyến mãi cho module đặt hàng. <see cref="EvaluateAsync"/> chỉ tính, không ghi gì; lượt dùng chỉ bị trừ khi
/// <see cref="RedeemAsync"/>. Redeem và Release idempotent theo <c>reference</c> (mã đơn).
/// Vi phạm nghiệp vụ ném <c>BusinessException</c> (<c>COUPON_INVALID</c>, <c>COUPON_EXPIRED</c>, ...).
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Chọn chương trình áp dụng cho giỏ: tối đa một chương trình giảm trên đơn và một chương trình giảm phí ship,
    /// mỗi loại lấy chương trình có lợi nhất cho khách. Mã giảm giá khách nhập sai/hết hạn thì ném lỗi; chương trình
    /// tự áp không đủ điều kiện thì bị bỏ qua.
    /// </summary>
    Task<PromotionEvaluation> EvaluateAsync(PromotionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhận lượt dùng cho đơn <paramref name="reference"/> (kiểm tra lại giới hạn trong khóa dòng).
    /// Hết lượt hoặc hết hạn giữa chừng thì ném lỗi và không ghi gì.
    /// </summary>
    Task RedeemAsync(
        string reference,
        PromotionEvaluation evaluation,
        PromotionCustomer customer,
        CancellationToken cancellationToken);

    /// <summary>Trả lại lượt dùng khi đơn bị hủy (hoặc lưu đơn thất bại).</summary>
    Task ReleaseAsync(string reference, CancellationToken cancellationToken);
}

/// <param name="UserId">null với khách vãng lai.</param>
/// <param name="Phone">Số điện thoại nhận hàng; khách vãng lai tính lượt dùng theo số này.</param>
public sealed record PromotionCustomer(long? UserId, string? Phone);

/// <param name="CategoryIds">Danh mục của sách, gồm cả danh mục cha.</param>
/// <param name="IsOnSale">Đang chạy lịch giá (giá bán thấp hơn giá bán thường).</param>
public sealed record PromotionLine(
    long SkuId,
    long BookId,
    IReadOnlyCollection<long> CategoryIds,
    decimal UnitPrice,
    int Quantity,
    bool IsOnSale);

/// <param name="ShippingFee">Phí ship khách phải trả trước khi giảm (0 nếu đã được miễn phí theo ngưỡng).</param>
/// <param name="CouponCode">Mã khách nhập, không phân biệt hoa thường; null nếu không dùng mã.</param>
public sealed record PromotionRequest(
    IReadOnlyList<PromotionLine> Lines,
    decimal ShippingFee,
    PromotionCustomer Customer,
    string? CouponCode);

public enum PromotionKind
{
    OrderDiscount,
    FreeShipping,
}

/// <param name="CouponId">null với chương trình tự áp.</param>
/// <param name="Amount">Số tiền giảm trên đơn (<c>OrderDiscount</c>) hoặc trên phí ship (<c>FreeShipping</c>).</param>
public sealed record AppliedPromotion(
    long PromotionId,
    string Name,
    PromotionKind Kind,
    long? CouponId,
    string? CouponCode,
    decimal Amount);

/// <param name="LineDiscounts">Phần giảm trên đơn phân bổ cho từng SKU (tổng bằng <paramref name="OrderDiscount"/>).</param>
/// <param name="CouponApplied">false khi khách nhập mã hợp lệ nhưng có chương trình khác lợi hơn nên mã không được dùng.</param>
public sealed record PromotionEvaluation(
    decimal OrderDiscount,
    decimal ShippingDiscount,
    IReadOnlyDictionary<long, decimal> LineDiscounts,
    IReadOnlyList<AppliedPromotion> Applied,
    bool CouponApplied)
{
    public static PromotionEvaluation None { get; } = new(
        0m, 0m, new Dictionary<long, decimal>(), [], CouponApplied: false);
}

namespace SachLuat.Modules.Promotion.Contracts;

/// <summary>
/// Khuyến mãi cho module đặt hàng. <see cref="EvaluateAsync"/> chỉ tính, không ghi gì; lượt dùng chỉ bị trừ khi
/// <see cref="RedeemAsync"/>. Redeem và Release idempotent theo <c>reference</c> (mã đơn).
/// Vi phạm nghiệp vụ ném <c>BusinessException</c> (<c>COUPON_INVALID</c>, <c>COUPON_EXPIRED</c>, ...).
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Chọn ưu đãi áp dụng cho giỏ: combo bộ sách (nếu giỏ có đủ bộ), cộng thêm tối đa một chương trình giảm trên đơn
    /// và một chương trình giảm phí ship, mỗi loại chương trình lấy cái có lợi nhất cho khách. Chương trình giảm trên
    /// đơn **cộng dồn** với combo và tính trên số tiền còn lại sau combo. Mã giảm giá khách nhập sai/hết hạn thì ném
    /// lỗi; chương trình tự áp không đủ điều kiện thì bị bỏ qua.
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
/// <param name="RegularPrice">
/// Giá bán thường (chưa áp lịch giá) của SKU. Không còn dùng cho trần tổng mức giảm (nay tính trên
/// <paramref name="UnitPrice"/> hiện hành); giữ để tương thích.
/// </param>
/// <param name="ListPrice">
/// Giá niêm yết (giá bìa) của SKU: combo tính tiền giảm so với giá này cho sách được các bộ phủ trọn; null là dùng
/// <paramref name="UnitPrice"/> (không có phần chênh niêm yết).
/// </param>
public sealed record PromotionLine(
    long SkuId,
    long BookId,
    IReadOnlyCollection<long> CategoryIds,
    decimal UnitPrice,
    int Quantity,
    bool IsOnSale,
    decimal? RegularPrice = null,
    decimal? ListPrice = null);

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

    /// <summary>Giá combo bộ sách cố định; chỉ xuất hiện trong <see cref="AppliedPromotion"/>, không phải loại chương trình.</summary>
    Combo,
}

/// <param name="PromotionId">Id chương trình; với <see cref="PromotionKind.Combo"/> là id combo.</param>
/// <param name="CouponId">null với chương trình tự áp và với combo.</param>
/// <param name="Amount">
/// Số tiền giảm trên đơn (<c>OrderDiscount</c>, <c>Combo</c>) hoặc trên phí ship (<c>FreeShipping</c>).
/// Mỗi combo được áp là một phần tử riêng, <c>Amount</c> là tổng giảm của tất cả các bộ của combo đó.
/// </param>
public sealed record AppliedPromotion(
    long PromotionId,
    string Name,
    PromotionKind Kind,
    long? CouponId,
    string? CouponCode,
    decimal Amount);

/// <param name="OrderDiscount">
/// Tổng giảm trên đơn, gồm giảm combo (so với giá niêm yết, đã cộng <see cref="PromotionEvaluation.ListMarkups"/>) và
/// chương trình giảm trên đơn.
/// </param>
/// <param name="LineDiscounts">
/// Phần giảm trên đơn phân bổ cho từng SKU (tổng bằng <paramref name="OrderDiscount"/>), gồm cả phần chênh niêm yết của
/// combo để <c>tiền dòng theo giá niêm yết − giảm</c> vẫn bằng số khách phải trả.
/// </param>
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

    /// <summary>
    /// Chênh lệch <c>giá niêm yết − giá hiện hành</c> của các cuốn được combo phủ trọn, theo SKU. Ordering cộng vào
    /// tiền dòng và tạm tính (dòng hiện theo giá niêm yết) để khớp với tiền giảm combo; số khách phải trả không đổi.
    /// </summary>
    public IReadOnlyDictionary<long, decimal> ListMarkups { get; init; } = new Dictionary<long, decimal>();
}

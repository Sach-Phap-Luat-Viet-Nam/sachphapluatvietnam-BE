using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Promotion.Domain;

internal static class PromotionErrors
{
    // Ba mã đầu theo mục 16.2.
    public const string CouponInvalidCode = "COUPON_INVALID";
    public const string CouponExpiredCode = "COUPON_EXPIRED";
    public const string CouponMinOrderCode = "COUPON_MIN_ORDER";
    public const string CouponNotApplicableCode = "COUPON_NOT_APPLICABLE";
    public const string CouponLoginRequiredCode = "COUPON_LOGIN_REQUIRED";
    public const string CouponCodeDuplicateCode = "COUPON_CODE_DUPLICATE";
    public const string PromotionNotCouponCode = "PROMOTION_NOT_COUPON";
    public const string PromotionUnavailableCode = "PROMOTION_UNAVAILABLE";

    public static BusinessException Invalid(string message) =>
        new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    public static BusinessException CouponInvalid() => new(CouponInvalidCode, "Mã giảm giá không hợp lệ");

    public static BusinessException CouponExpired() =>
        new(CouponExpiredCode, "Mã giảm giá đã hết hạn hoặc hết lượt");

    public static BusinessException CouponCustomerLimit() =>
        new(CouponExpiredCode, "Bạn đã dùng hết số lượt của mã giảm giá này");

    public static BusinessException CouponMinOrder(decimal minOrder) => new(
        CouponMinOrderCode, $"Đơn chưa đạt giá trị tối thiểu {minOrder:N0}đ để dùng mã");

    public static BusinessException CouponNotApplicable() =>
        new(CouponNotApplicableCode, "Mã giảm giá không áp dụng cho các sách trong giỏ");

    public static BusinessException CouponLoginRequired() =>
        new(CouponLoginRequiredCode, "Vui lòng đăng nhập để dùng mã giảm giá này");

    public static BusinessException CouponCodeDuplicate(string code) =>
        new(CouponCodeDuplicateCode, $"Mã \"{code}\" đã tồn tại", StatusCodes.Status409Conflict);

    public static BusinessException PromotionNotCoupon() => new(
        PromotionNotCouponCode, "Chỉ chương trình dùng mã giảm giá mới có thể thêm mã");

    /// <summary>Chương trình tự áp hết lượt hoặc kết thúc giữa lúc khách đang đặt hàng.</summary>
    public static BusinessException PromotionUnavailable() => new(
        PromotionUnavailableCode, "Chương trình khuyến mãi đã kết thúc hoặc hết lượt, vui lòng kiểm tra lại giá");

    public static BusinessException PromotionNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy chương trình khuyến mãi", StatusCodes.Status404NotFound);

    public static BusinessException CouponNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy mã giảm giá", StatusCodes.Status404NotFound);
}

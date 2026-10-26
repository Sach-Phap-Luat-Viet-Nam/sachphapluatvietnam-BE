using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Ordering.Domain;

internal static class OrderErrors
{
    public const string InvalidTransitionCode = "ORDER_INVALID_TRANSITION";
    public const string NotCancellableCode = "ORDER_NOT_CANCELLABLE";
    public const string CartEmptyCode = "CART_EMPTY";
    public const string SkuNotAvailableCode = "SKU_NOT_AVAILABLE";
    public const string MaxQuantityExceededCode = "MAX_QTY_EXCEEDED";
    public const string PaymentMethodNotSupportedCode = "PAYMENT_METHOD_NOT_SUPPORTED";
    public const string IdempotencyKeyReusedCode = "IDEMPOTENCY_KEY_REUSED";
    public const string OrderNotPaidCode = "ORDER_NOT_PAID";
    public const string CheckoutTotalChangedCode = "CHECKOUT_TOTAL_CHANGED";
    public const string TooManyOpenOrdersCode = "TOO_MANY_OPEN_ORDERS";
    public const string CodNotAllowedCode = "COD_NOT_ALLOWED";
    public const string SepayUnauthorizedCode = "SEPAY_UNAUTHORIZED";

    public static BusinessException InvalidTransition(OrderStatus from, OrderStatus to) => new(
        InvalidTransitionCode,
        $"Không thể chuyển đơn từ trạng thái {from} sang {to}",
        StatusCodes.Status409Conflict);

    /// <summary>Mã lỗi và thông báo theo mục 16.2.</summary>
    public static BusinessException NotCancellable() =>
        new(NotCancellableCode, "Đơn đã được đóng gói, không thể hủy");

    public static BusinessException CartEmpty() => new(CartEmptyCode, "Giỏ hàng đang trống");

    public static BusinessException SkuNotAvailable() => new(
        SkuNotAvailableCode, "Giỏ hàng có sách hiện không bán, vui lòng xóa khỏi giỏ trước khi đặt hàng");

    public static BusinessException BuyNowSkuNotAvailable() => new(SkuNotAvailableCode, "Sách này hiện không bán");

    public static BusinessException MaxQuantityExceeded(int max) => new(
        MaxQuantityExceededCode, $"Tối đa {max} cuốn mỗi loại, vui lòng gửi yêu cầu báo giá sỉ");

    public static BusinessException PaymentMethodNotSupported() => new(
        PaymentMethodNotSupportedCode, "Phương thức thanh toán chưa được hỗ trợ");

    public static BusinessException IdempotencyKeyReused() => new(
        IdempotencyKeyReusedCode, "Idempotency-Key đã được dùng cho đơn khác", StatusCodes.Status409Conflict);

    public static BusinessException OrderNotPaid() =>
        new(OrderNotPaidCode, "Đơn chưa thanh toán nên không thể hoàn tiền");

    public static BusinessException CheckoutTotalChanged() => new(
        CheckoutTotalChangedCode,
        "Giá hoặc ưu đãi của đơn hàng đã thay đổi, vui lòng kiểm tra lại tổng tiền trước khi đặt hàng",
        StatusCodes.Status409Conflict);

    public static BusinessException TooManyOpenOrders(int max) => new(
        TooManyOpenOrdersCode,
        $"Số điện thoại này đang có {max} đơn thanh toán khi nhận hàng chưa giao xong, vui lòng chờ nhận hàng "
        + "hoặc chọn chuyển khoản");

    public static BusinessException CodNotAllowed() => new(
        CodNotAllowedCode,
        "Số điện thoại này hiện không dùng được thanh toán khi nhận hàng, vui lòng chọn chuyển khoản");

    public static BusinessException SepayUnauthorized() => new(
        SepayUnauthorizedCode, "API key SePay không hợp lệ", StatusCodes.Status401Unauthorized);

    public static BusinessException NotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy đơn hàng", StatusCodes.Status404NotFound);
}

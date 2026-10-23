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

    public static BusinessException MaxQuantityExceeded(int max) => new(
        MaxQuantityExceededCode, $"Tối đa {max} cuốn mỗi loại, vui lòng gửi yêu cầu báo giá sỉ");

    public static BusinessException PaymentMethodNotSupported() => new(
        PaymentMethodNotSupportedCode, "Phương thức thanh toán chưa được hỗ trợ");

    public static BusinessException IdempotencyKeyReused() => new(
        IdempotencyKeyReusedCode, "Idempotency-Key đã được dùng cho đơn khác", StatusCodes.Status409Conflict);

    public static BusinessException OrderNotPaid() =>
        new(OrderNotPaidCode, "Đơn chưa thanh toán nên không thể hoàn tiền");

    public static BusinessException NotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy đơn hàng", StatusCodes.Status404NotFound);
}

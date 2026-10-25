using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Ordering.Application;

internal sealed record ShippingAddressDto(string Province, string Ward, string StreetAddress);

internal sealed record InvoiceInfoDto(string CompanyName, string TaxCode, string Address, string Email);

/// <summary>Thông tin người nhận và thanh toán, dùng chung cho đặt từ giỏ và "Mua ngay".</summary>
internal interface ICheckoutDetails
{
    string CustomerName { get; }

    string Phone { get; }

    string? Email { get; }

    ShippingAddressDto ShippingAddress { get; }

    PaymentMethod PaymentMethod { get; }

    InvoiceInfoDto? InvoiceInfo { get; }

    string? Note { get; }

    /// <summary>Mã giảm giá khách nhập (không phân biệt hoa thường).</summary>
    string? CouponCode { get; }

    /// <summary>
    /// Tổng tiền khách đã thấy ở bước xem trước. Khác tổng tính lại lúc đặt (chương trình hết hạn, giá đổi, ...)
    /// thì trả 409 <c>CHECKOUT_TOTAL_CHANGED</c> thay vì thu số tiền khách chưa đồng ý.
    /// </summary>
    decimal? ExpectedTotal { get; }
}

/// <summary>Tạo đơn từ giỏ hàng hiện tại (FR-33: không bắt buộc đăng nhập).</summary>
internal sealed record CreateOrderRequest(
    string CustomerName,
    string Phone,
    string? Email,
    ShippingAddressDto ShippingAddress,
    PaymentMethod PaymentMethod,
    InvoiceInfoDto? InvoiceInfo,
    string? Note,
    string? CouponCode = null,
    decimal? ExpectedTotal = null) : ICheckoutDetails;

/// <summary>"Mua ngay": đặt thẳng một SKU, không đọc và không xóa giỏ hàng.</summary>
internal sealed record BuyNowOrderRequest(
    long SkuId,
    int Quantity,
    string CustomerName,
    string Phone,
    string? Email,
    ShippingAddressDto ShippingAddress,
    PaymentMethod PaymentMethod,
    InvoiceInfoDto? InvoiceInfo,
    string? Note,
    string? CouponCode = null,
    decimal? ExpectedTotal = null) : ICheckoutDetails;

internal sealed record LookupOrderRequest(string OrderCode, string Phone);

internal sealed record CancelOrderRequest(string? Reason);

internal sealed record ChangeOrderStatusRequest(OrderStatus Status, string? Note);

/// <param name="Subtotal">Tạm tính trước giảm giá, dùng so với ngưỡng miễn phí.</param>
/// <param name="Province">Bỏ trống thì dùng phí cố định (hành vi cũ khi chưa có địa chỉ).</param>
/// <param name="Items">Dùng tính trọng lượng; bỏ trống thì chỉ tính bao bì.</param>
internal sealed record ShippingQuoteRequest(
    decimal Subtotal,
    string? Province = null,
    IReadOnlyList<QuoteItem>? Items = null);

internal sealed record QuoteItem(long SkuId, int Quantity);

/// <param name="FreeShippingThreshold">null: không miễn phí ship theo ngưỡng.</param>
internal sealed record ShippingQuoteResponse(
    decimal ShippingFee,
    decimal? FreeShippingThreshold,
    decimal AmountToFreeShipping,
    EtaResponse? Eta,
    string? ZoneName);

internal sealed record EtaResponse(int MinDays, int MaxDays);

internal enum CheckoutSource
{
    Cart,
    BuyNow,
}

/// <param name="SkuId">Chỉ dùng với <c>buy_now</c>.</param>
/// <param name="Province">Bỏ trống thì dùng phí ship cố định.</param>
/// <param name="Phone">Tùy chọn; để kiểm tra giới hạn lượt dùng mã theo số điện thoại với khách vãng lai.</param>
internal sealed record CheckoutPreviewRequest(
    CheckoutSource Source,
    long? SkuId,
    int? Quantity,
    string? Province,
    string? CouponCode,
    string? Phone);

internal sealed record PricedItemResponse(
    long SkuId,
    string SkuCode,
    string BookTitle,
    string? EditionName,
    string CoverType,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    decimal DiscountAmount);

internal sealed record AppliedPromotionResponse(
    long PromotionId,
    string Name,
    string Kind,
    string? CouponCode,
    decimal Amount);

/// <summary>Tính tiền xem trước ở trang thanh toán; cùng cách tính với lúc tạo đơn.</summary>
/// <param name="ShippingFee">Phí ship sau miễn phí theo ngưỡng, trước giảm giá ship.</param>
/// <param name="CouponApplied">null khi khách không nhập mã; false khi mã hợp lệ nhưng có ưu đãi khác lợi hơn.</param>
/// <param name="AmountToFreeShipping">Còn thiếu bao nhiêu để được miễn phí ship; 0 khi đã đạt hoặc không có ngưỡng.</param>
/// <param name="CodAllowed">false khi số điện thoại gửi kèm đang bị khóa COD hoặc đủ số đơn COD đang mở; FE ẩn COD.</param>
internal sealed record CheckoutPreviewResponse(
    IReadOnlyList<PricedItemResponse> Items,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal ShippingDiscount,
    decimal Total,
    IReadOnlyList<AppliedPromotionResponse> AppliedPromotions,
    bool? CouponApplied,
    decimal? FreeShippingThreshold,
    decimal AmountToFreeShipping,
    EtaResponse? Eta,
    string? ZoneName,
    bool CodAllowed);

internal sealed class AdminOrdersQuery : PagedQuery
{
    /// <summary>Ví dụ <c>pending_confirm</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Ví dụ <c>cod</c>, <c>bank_transfer</c>.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Lọc theo ngày tạo (giờ Việt Nam), tính cả hai đầu.</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>Mã đơn hoặc số điện thoại.</summary>
    public string? Q { get; set; }
}

internal sealed record OrderItemResponse(
    long SkuId,
    string SkuCode,
    string BookTitle,
    string? EditionName,
    string Isbn,
    string CoverType,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    decimal DiscountAmount);

internal sealed record OrderStatusLogResponse(
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    string? Note,
    DateTime CreatedAt);

/// <param name="GuestAccessToken">
/// Chỉ có khi khách vãng lai vừa đặt đơn (kể cả gửi lại cùng <c>Idempotency-Key</c>): FE dựng link xem và hủy đơn.
/// </param>
internal sealed record OrderResponse(
    string OrderCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string CustomerName,
    string Phone,
    string? Email,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal ShippingDiscount,
    decimal Total,
    IReadOnlyList<AppliedPromotionResponse> AppliedPromotions,
    bool InvoiceRequired,
    InvoiceInfoDto? InvoiceInfo,
    string? Note,
    DateTime? PaymentExpiresAt,
    bool CanCancel,
    DateTime CreatedAt,
    IReadOnlyList<OrderStatusLogResponse> History,
    string? GuestAccessToken);

internal sealed record OrderSummaryResponse(
    string OrderCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string CustomerName,
    string Phone,
    int ItemCount,
    decimal Total,
    DateTime CreatedAt);

/// <param name="CostOfGoods">Tổng giá vốn (có sau khi đóng gói), dùng cho báo cáo lợi nhuận gộp (FR-95).</param>
/// <param name="CustomerRisk">Lịch sử đơn theo số điện thoại của đơn, để CSKH cân nhắc khi gọi xác nhận.</param>
internal sealed record AdminOrderResponse(
    OrderResponse Order,
    long? UserId,
    IReadOnlyList<OrderStatus> NextStatuses,
    decimal? CostOfGoods,
    CustomerRiskResponse CustomerRisk);

/// <summary>Số liệu theo số điện thoại (mọi đơn, kể cả đơn hiện tại).</summary>
/// <param name="FailedCodCount">Đơn COD giao thất bại (từng sang <c>returning</c>).</param>
/// <param name="OpenCodCount">Đơn COD chưa giao xong (từ <c>pending_confirm</c> đến <c>shipping</c>).</param>
/// <param name="CodBlocked">Đạt ngưỡng giao thất bại: chỉ được chuyển khoản.</param>
/// <param name="OpenCodLimitReached">Đủ số đơn COD đang mở: không đặt thêm COD cho tới khi có đơn giao xong.</param>
internal sealed record CustomerRiskResponse(
    int TotalOrders,
    int DeliveredCount,
    int CancelledCount,
    int FailedCodCount,
    int OpenCodCount,
    bool CodBlocked,
    bool OpenCodLimitReached)
{
    public bool CodAllowed => !CodBlocked && !OpenCodLimitReached;
}

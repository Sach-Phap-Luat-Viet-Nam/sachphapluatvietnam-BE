using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Ordering.Application;

internal sealed record ShippingAddressDto(string Province, string Ward, string StreetAddress);

internal sealed record InvoiceInfoDto(string CompanyName, string TaxCode, string Address, string Email);

/// <summary>Tạo đơn từ giỏ hàng hiện tại (FR-33: không bắt buộc đăng nhập).</summary>
internal sealed record CreateOrderRequest(
    string CustomerName,
    string Phone,
    string? Email,
    ShippingAddressDto ShippingAddress,
    PaymentMethod PaymentMethod,
    InvoiceInfoDto? InvoiceInfo,
    string? Note);

internal sealed record LookupOrderRequest(string OrderCode, string Phone);

internal sealed record CancelOrderRequest(string? Reason);

internal sealed record ChangeOrderStatusRequest(OrderStatus Status, string? Note);

internal sealed record ShippingQuoteRequest(decimal Subtotal);

internal sealed record ShippingQuoteResponse(decimal ShippingFee, decimal FreeShippingThreshold);

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
    decimal LineTotal);

internal sealed record OrderStatusLogResponse(
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    string? Note,
    DateTime CreatedAt);

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
    decimal Total,
    bool InvoiceRequired,
    InvoiceInfoDto? InvoiceInfo,
    string? Note,
    DateTime? PaymentExpiresAt,
    bool CanCancel,
    DateTime CreatedAt,
    IReadOnlyList<OrderStatusLogResponse> History);

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
internal sealed record AdminOrderResponse(
    OrderResponse Order,
    long? UserId,
    IReadOnlyList<OrderStatus> NextStatuses,
    decimal? CostOfGoods);

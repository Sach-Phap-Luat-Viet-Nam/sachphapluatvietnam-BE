using System.Buffers.Text;
using System.Security.Cryptography;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Ordering.Domain;

internal sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = [];
    private readonly List<AppliedPromotionSnapshot> _appliedPromotions = [];
    private readonly List<OrderStatusLog> _statusLogs = [];

    private Order(
        string orderCode,
        long? userId,
        string customerName,
        string phone,
        string? email,
        ShippingAddress shippingAddress,
        PaymentMethod paymentMethod,
        InvoiceInfo? invoiceInfo,
        string? note,
        string? idempotencyKey)
    {
        OrderCode = orderCode;
        UserId = userId;
        CustomerName = customerName;
        Phone = PhoneNumbers.Normalize(phone);
        Email = email;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        PaymentStatus = PaymentStatus.Unpaid;
        OrderType = OrderType.Retail;
        InvoiceRequired = invoiceInfo is not null;
        InvoiceInfo = invoiceInfo;
        Note = note;
        IdempotencyKey = idempotencyKey;
        GuestAccessToken = userId is null ? NewGuestAccessToken() : null;
    }

    // EF Core
    private Order()
    {
    }

    public string OrderCode { get; private set; } = string.Empty;

    /// <summary>Null với khách vãng lai (FR-33).</summary>
    public long? UserId { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    // Owned type bắt buộc: constructor luôn gán, EF Core luôn nạp khi đọc đơn; null! chỉ cho constructor rỗng của EF.
    public ShippingAddress ShippingAddress { get; private set; } = null!;

    public decimal Subtotal { get; private set; }

    public decimal Discount { get; private set; }

    public decimal ShippingFee { get; private set; }

    /// <summary>Phí theo biểu giá trước khi miễn phí theo ngưỡng; <c>ShippingBaseFee - ShippingFee</c> là phần doanh nghiệp bù.</summary>
    public decimal ShippingBaseFee { get; private set; }

    /// <summary>Giảm trên phí ship từ chương trình khuyến mãi; khách trả <c>ShippingFee - ShippingDiscount</c>.</summary>
    public decimal ShippingDiscount { get; private set; }

    public decimal Total { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public OrderStatus Status { get; private set; }

    public OrderType OrderType { get; private set; }

    public bool InvoiceRequired { get; private set; }

    public InvoiceInfo? InvoiceInfo { get; private set; }

    public string? Note { get; private set; }

    public string? IdempotencyKey { get; private set; }

    /// <summary>
    /// Chỉ đơn khách vãng lai có: chuỗi ngẫu nhiên 128 bit trong link xem và hủy đơn không cần đăng nhập.
    /// Mã đơn tuần tự + SĐT dễ dò nên không dùng làm khóa truy cập.
    /// </summary>
    public string? GuestAccessToken { get; private set; }

    /// <summary>Hạn thanh toán online; quá hạn thì job tự hủy và hoàn tồn tạm giữ (BR-10 bước 4).</summary>
    public DateTime? PaymentExpiresAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public IReadOnlyCollection<AppliedPromotionSnapshot> AppliedPromotions => _appliedPromotions;

    public IReadOnlyCollection<OrderStatusLog> StatusLogs => _statusLogs;

    /// <summary>
    /// Tạo đơn: COD → <c>pending_confirm</c>; thanh toán trước → <c>pending_payment</c> kèm hạn thanh toán.
    /// </summary>
    public static Order Create(
        string orderCode,
        long? userId,
        string customerName,
        string phone,
        string? email,
        ShippingAddress shippingAddress,
        PaymentMethod paymentMethod,
        InvoiceInfo? invoiceInfo,
        string? note,
        string? idempotencyKey,
        IReadOnlyCollection<OrderItem> items,
        OrderCharges charges,
        DateTime now,
        TimeSpan paymentTimeout)
    {
        var order = new Order(
            orderCode, userId, customerName, phone, email, shippingAddress, paymentMethod, invoiceInfo, note,
            idempotencyKey);
        order._items.AddRange(items);
        order.Subtotal = items.Sum(item => item.LineTotal);

        // Tổng tiền là nguồn sự thật của khoản khách trả, nên bất biến sai là lỗi lập trình, không phải lỗi người dùng.
        if (charges.Discount != items.Sum(item => item.DiscountAmount)
            || charges.Discount > order.Subtotal
            || charges.ShippingDiscount > charges.ShippingFee
            || charges.ShippingBaseFee < charges.ShippingFee)
        {
            throw new ArgumentException("Các khoản giảm giá và phí ship của đơn không nhất quán", nameof(charges));
        }

        order.Discount = charges.Discount;
        order.ShippingFee = charges.ShippingFee;
        order.ShippingBaseFee = charges.ShippingBaseFee;
        order.ShippingDiscount = charges.ShippingDiscount;
        order._appliedPromotions.AddRange(charges.AppliedPromotions);
        order.Total = order.Subtotal - order.Discount + order.ShippingFee - order.ShippingDiscount;

        if (paymentMethod == PaymentMethod.Cod)
        {
            order.Status = OrderStatus.PendingConfirm;
        }
        else
        {
            order.Status = OrderStatus.PendingPayment;
            order.PaymentExpiresAt = now.Add(paymentTimeout);
        }

        order._statusLogs.Add(new OrderStatusLog(null, order.Status, userId, null, now));
        return order;
    }

    public bool CanTransitionTo(OrderStatus target) => OrderStatusMachine.CanTransition(Status, target);

    public void ChangeStatus(OrderStatus target, long? changedBy, string? note, DateTime now)
    {
        if (!CanTransitionTo(target))
        {
            throw OrderErrors.InvalidTransition(Status, target);
        }

        if (target == OrderStatus.Refunded && PaymentStatus != PaymentStatus.Paid)
        {
            throw OrderErrors.OrderNotPaid();
        }

        _statusLogs.Add(new OrderStatusLog(Status, target, changedBy, note, now));
        Status = target;

        PaymentStatus = target switch
        {
            OrderStatus.Paid => PaymentStatus.Paid,
            OrderStatus.Refunded => PaymentStatus.Refunded,
            // COD: thu tiền khi giao thành công; đối soát với đơn vị vận chuyển làm ở FR-74.
            OrderStatus.Delivered when PaymentMethod == PaymentMethod.Cod => PaymentStatus.Paid,
            _ => PaymentStatus,
        };

        if (target != OrderStatus.PendingPayment)
        {
            PaymentExpiresAt = null;
        }
    }

    public void SetCostPrices(IReadOnlyDictionary<long, decimal> costPrices)
    {
        foreach (var item in _items)
        {
            if (costPrices.TryGetValue(item.SkuId, out var cost))
            {
                item.SetCostPrice(cost);
            }
        }
    }

    private static string NewGuestAccessToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}

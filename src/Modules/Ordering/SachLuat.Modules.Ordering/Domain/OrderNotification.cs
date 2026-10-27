namespace SachLuat.Modules.Ordering.Domain;

internal enum OrderNotificationKind
{
    /// <summary>Đơn mới không cần chờ thanh toán (COD).</summary>
    NewOrder,

    /// <summary>Đơn chuyển khoản vừa được SePay xác nhận đã nhận tiền.</summary>
    Paid,

    /// <summary>Khách (thành viên hoặc vãng lai) tự hủy đơn.</summary>
    CancelledByCustomer,
}

internal enum OrderNotificationStatus
{
    Pending,
    Sent,

    /// <summary>Hết số lần thử; cần xem log, không tự gửi lại.</summary>
    Failed,
}

/// <summary>
/// Outbox tin nhắn báo nhân viên (group Zalo). Ghi cùng transaction với thay đổi của đơn nên không mất tin khi kênh
/// gửi lỗi hay app khởi động lại; <c>OrderNotificationDispatcher</c> gửi và thử lại sau.
/// </summary>
internal sealed class OrderNotification
{
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    public OrderNotification(Order order, OrderNotificationKind kind, DateTime now)
    {
        Order = order;
        Kind = kind;
        Status = OrderNotificationStatus.Pending;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    // EF Core
    private OrderNotification()
    {
    }

    public long Id { get; private set; }

    public long OrderId { get; private set; }

    // Chỉ để EF gán OrderId khi đơn và thông báo được lưu cùng lúc (đơn mới chưa có Id).
    public Order? Order { get; private set; }

    public OrderNotificationKind Kind { get; private set; }

    public OrderNotificationStatus Status { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Lần gửi kế tiếp; null khi đã gửi xong hoặc bỏ cuộc.</summary>
    public DateTime? NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Giữ chỗ trước khi gửi để instance khác không gửi trùng; app chết giữa chừng thì hết hạn và gửi lại.
    /// </summary>
    public void Claim(DateTime now, TimeSpan lease) => NextAttemptAt = now.Add(lease);

    public void MarkSent(DateTime now)
    {
        Status = OrderNotificationStatus.Sent;
        Attempts++;
        SentAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>Lùi lần sau theo cấp số nhân (30 giây, 1 phút, 2 phút... tối đa 1 giờ).</summary>
    public void MarkFailed(string error, DateTime now, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;
        if (Attempts >= maxAttempts)
        {
            Status = OrderNotificationStatus.Failed;
            NextAttemptAt = null;
            return;
        }

        var delay = FirstRetryDelay * Math.Pow(2, Attempts - 1);
        NextAttemptAt = now.Add(delay < MaxRetryDelay ? delay : MaxRetryDelay);
    }
}

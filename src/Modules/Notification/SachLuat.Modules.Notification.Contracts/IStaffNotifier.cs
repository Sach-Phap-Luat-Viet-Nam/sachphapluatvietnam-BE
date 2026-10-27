namespace SachLuat.Modules.Notification.Contracts;

/// <summary>Gửi tin nhắn nội bộ cho nhân viên (group Zalo) để module khác dùng, ví dụ báo đơn hàng mới.</summary>
public interface IStaffNotifier
{
    /// <summary>Đã cấu hình kênh gửi; <c>false</c> thì bên gọi không nên xếp hàng tin nhắn.</summary>
    bool IsEnabled { get; }

    /// <summary>Gửi một tin nhắn văn bản; nội dung quá giới hạn của kênh sẽ bị cắt bớt.</summary>
    /// <exception cref="StaffNotificationException">
    /// Kênh gửi lỗi hoặc từ chối; bên gọi tự quyết định thử lại.
    /// </exception>
    Task SendAsync(string text, CancellationToken cancellationToken);
}

/// <summary>Gửi tin nhắn nội bộ thất bại (mạng, quá thời gian chờ, API của kênh từ chối).</summary>
public sealed class StaffNotificationException : Exception
{
    public StaffNotificationException(string message)
        : base(message)
    {
    }

    public StaffNotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

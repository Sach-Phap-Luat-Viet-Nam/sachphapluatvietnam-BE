namespace SachLuat.Modules.Auth.Infrastructure.Realtime;

/// <summary>Cấu hình Pusher Channels (<c>Pusher:*</c>). Để trống thì tắt realtime: không bắn sự kiện, API ký kênh trả 503.</summary>
internal sealed class RealtimeOptions
{
    public const string SectionName = "Pusher";

    public string AppId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    /// <summary>Bí mật: user-secrets / biến môi trường (<c>Pusher__Secret</c>), không commit.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Cluster của app Pusher, ví dụ <c>ap1</c>.</summary>
    public string Cluster { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppId)
        && !string.IsNullOrWhiteSpace(Key)
        && !string.IsNullOrWhiteSpace(Secret)
        && !string.IsNullOrWhiteSpace(Cluster);
}

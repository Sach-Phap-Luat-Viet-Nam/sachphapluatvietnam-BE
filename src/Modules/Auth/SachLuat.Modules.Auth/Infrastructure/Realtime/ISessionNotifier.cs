using System.Text.Json;

namespace SachLuat.Modules.Auth.Infrastructure.Realtime;

/// <summary>Lý do một phiên đăng nhập bị kết thúc, gửi cho FE để hiện thông báo.</summary>
internal enum SessionEndReason
{
    /// <summary>Tài khoản vừa đăng nhập ở nơi khác (mỗi tài khoản chỉ đăng nhập một nơi).</summary>
    SignedInElsewhere,

    /// <summary>Quản trị viên buộc đăng xuất (hoặc đặt lại mật khẩu, vô hiệu hóa).</summary>
    ForcedLogout,
}

internal static class RealtimeChannels
{
    public const string SessionEndedEvent = "session-ended";

    /// <summary>Kênh riêng của một tài khoản; phải qua <c>POST /realtime/auth</c> mới đăng ký nghe được.</summary>
    public static string ForUser(long userId) => $"private-user-{userId}";

    public static string ReasonCode(SessionEndReason reason) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(reason.ToString());
}

/// <summary>Báo cho các thiết bị đang mở của một tài khoản rằng phiên của chúng đã bị kết thúc.</summary>
internal interface ISessionNotifier
{
    /// <summary>
    /// Phát sự kiện <c>session-ended</c> kèm <paramref name="sessionVersion"/> mới. Thiết bị nào có phiên bản nhỏ hơn thì
    /// kết thúc phiên; thiết bị vừa đăng nhập mang đúng phiên bản này nên bỏ qua. Không ném lỗi khi gửi thất bại.
    /// </summary>
    Task NotifySessionEndedAsync(
        long userId,
        int sessionVersion,
        SessionEndReason reason,
        CancellationToken cancellationToken);
}

/// <summary>Dùng khi chưa cấu hình Pusher: không làm gì (phiên vẫn bị thu hồi, chỉ không có thông báo tức thì).</summary>
internal sealed class NullSessionNotifier : ISessionNotifier
{
    public Task NotifySessionEndedAsync(
        long userId,
        int sessionVersion,
        SessionEndReason reason,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

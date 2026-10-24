using Microsoft.Extensions.Logging;
using PusherServer;
using PusherServer.Exceptions;

namespace SachLuat.Modules.Auth.Infrastructure.Realtime;

internal sealed class PusherSessionNotifier(IPusher pusher, ILogger<PusherSessionNotifier> logger) : ISessionNotifier
{
    // Pusher chậm hoặc không tới được thì không giữ request đăng nhập/buộc đăng xuất lâu hơn mức này.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(3);

    public async Task NotifySessionEndedAsync(
        long userId,
        int sessionVersion,
        SessionEndReason reason,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        try
        {
            var result = await pusher
                .TriggerAsync(
                    RealtimeChannels.ForUser(userId),
                    RealtimeChannels.SessionEndedEvent,
                    new { reason = RealtimeChannels.ReasonCode(reason), version = sessionVersion })
                .WaitAsync(timeout.Token);
            logger.LogInformation(
                "Sent {Event} to user {UserId} ({Reason}, version {Version}), status {StatusCode}",
                RealtimeChannels.SessionEndedEvent, userId, reason, sessionVersion, result.StatusCode);
        }
        catch (Exception exception) when (
            exception is TriggerResponseException or HttpRequestException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Thông báo là phần thêm: phiên đã bị thu hồi ở server, FE cũ vẫn bị 401 ở lần gọi API kế tiếp.
            logger.LogWarning(exception, "Could not send {Event} to user {UserId}", RealtimeChannels.SessionEndedEvent, userId);
        }
    }
}

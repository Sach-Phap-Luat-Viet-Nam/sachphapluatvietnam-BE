using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Xếp hàng tin nhắn báo nhân viên vào outbox; bên gọi tự <c>SaveChanges</c> cùng transaction với thay đổi của đơn.
/// </summary>
internal sealed class OrderNotificationOutbox(
    OrderingDbContext dbContext,
    IStaffNotifier staffNotifier,
    TimeProvider timeProvider)
{
    public void Enqueue(Order order, OrderNotificationKind kind)
    {
        // Chưa cấu hình kênh gửi thì không xếp hàng, tránh dồn tin cũ gửi một loạt khi bật lên sau này.
        if (!staffNotifier.IsEnabled)
        {
            return;
        }

        dbContext.OrderNotifications.Add(new OrderNotification(order, kind, timeProvider.GetUtcNow().UtcDateTime));
    }
}

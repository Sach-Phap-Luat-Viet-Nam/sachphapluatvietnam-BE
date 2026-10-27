using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Định kỳ gửi các tin trong outbox <c>order_notifications</c> tới nhân viên; gửi lỗi thì lùi dần và thử lại.
/// </summary>
/// <remarks>
/// Mỗi lượt giữ chỗ một lô bằng <c>FOR UPDATE SKIP LOCKED</c> rồi commit ngay, không giữ transaction trong lúc gọi
/// API bên ngoài; nhiều instance chạy cùng lúc không gửi trùng.
/// </remarks>
internal sealed class OrderNotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderingOptions> options,
    TimeProvider timeProvider,
    ILogger<OrderNotificationDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 10;

    // Đủ cho cả lô gửi xong kể cả khi từng tin chờ hết timeout của kênh gửi.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    private readonly OrderingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableStaffNotificationDispatch)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.StaffNotificationPollSeconds), timeProvider);
        do
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Tầng biên của job nền: ghi log và chờ lượt sau, không để job dừng hẳn.
                logger.LogError(exception, "Order notification dispatch failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <returns>Số tin đã gửi thành công trong lượt này.</returns>
    internal async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var staffNotifier = scope.ServiceProvider.GetRequiredService<IStaffNotifier>();
        if (!staffNotifier.IsEnabled)
        {
            return 0;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var claimed = await ClaimAsync(dbContext, cancellationToken);
        if (claimed.Count == 0)
        {
            return 0;
        }

        var orderIds = claimed.Select(notification => notification.OrderId).Distinct().ToList();
        var orders = await dbContext.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.StatusLogs)
            .AsSplitQuery()
            .Where(order => orderIds.Contains(order.Id))
            .ToDictionaryAsync(order => order.Id, cancellationToken);

        var sent = 0;
        foreach (var notification in claimed)
        {
            var order = orders[notification.OrderId];
            try
            {
                await staffNotifier.SendAsync(
                    OrderNotificationFormatter.Format(notification.Kind, order, _options.AdminBaseUrl),
                    cancellationToken);
                notification.MarkSent(timeProvider.GetUtcNow().UtcDateTime);
                sent++;
            }
            catch (StaffNotificationException exception)
            {
                notification.MarkFailed(
                    exception.Message, timeProvider.GetUtcNow().UtcDateTime, _options.StaffNotificationMaxAttempts);
                LogFailure(notification, order.OrderCode, exception);
            }

            // Lưu từng tin: app dừng giữa lô thì các tin đã gửi không bị gửi lại.
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (sent > 0)
        {
            logger.LogInformation("Sent {Count} order notifications to staff", sent);
        }

        return sent;
    }

    private async Task<List<OrderNotification>> ClaimAsync(
        OrderingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await dbContext.OrderNotifications
            .FromSql($"""
                SELECT * FROM ordering.order_notifications
                WHERE status = 'pending' AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
        foreach (var notification in claimed)
        {
            notification.Claim(now, ClaimLease);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private void LogFailure(OrderNotification notification, string orderCode, StaffNotificationException exception)
    {
        if (notification.Status == OrderNotificationStatus.Failed)
        {
            logger.LogError(
                exception,
                "Gave up {Kind} notification for order {OrderCode} after {Attempts} attempts",
                notification.Kind, orderCode, notification.Attempts);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Could not send {Kind} notification for order {OrderCode}, attempt {Attempts}, next {NextAttemptAt}",
                notification.Kind, orderCode, notification.Attempts, notification.NextAttemptAt);
        }
    }
}

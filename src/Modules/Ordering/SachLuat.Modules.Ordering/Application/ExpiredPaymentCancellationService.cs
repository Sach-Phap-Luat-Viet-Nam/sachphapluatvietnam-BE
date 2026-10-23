using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// BR-10 bước 4: định kỳ hủy đơn <c>pending_payment</c> quá hạn và hoàn tồn tạm giữ.
/// Chạy nhiều instance cùng lúc vẫn an toàn vì mỗi đơn được khóa dòng khi đổi trạng thái.
/// </summary>
internal sealed class ExpiredPaymentCancellationService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderingOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredPaymentCancellationService> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const string CancelNote = "Tự động hủy do quá hạn thanh toán";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableExpiredPaymentSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.ExpiredPaymentSweepSeconds), timeProvider);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Tầng biên của job nền: ghi log và chờ lượt sau, không để job dừng hẳn.
                logger.LogError(exception, "Expired payment sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        List<long> expiredIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            expiredIds = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Orders
                .Where(order => order.Status == OrderStatus.PendingPayment && order.PaymentExpiresAt < now)
                .OrderBy(order => order.PaymentExpiresAt)
                .Select(order => order.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        var cancelled = 0;
        foreach (var orderId in expiredIds)
        {
            // Scope riêng mỗi đơn: DbContext sạch, lỗi một đơn không ảnh hưởng đơn khác.
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<OrderStatusService>().ChangeAsync(
                    orderId, OrderStatus.Cancelled, changedBy: null, CancelNote, guard: null, cancellationToken);
                cancelled++;
            }
            catch (BusinessException exception)
            {
                // Đơn vừa được thanh toán/xử lý ngay trước khi job khóa được: bỏ qua.
                logger.LogDebug("Skip expired order {OrderId}: {ErrorCode}", orderId, exception.Code);
            }
        }

        if (cancelled > 0)
        {
            logger.LogInformation("Cancelled {Count} orders with expired payment", cancelled);
        }

        return cancelled;
    }
}

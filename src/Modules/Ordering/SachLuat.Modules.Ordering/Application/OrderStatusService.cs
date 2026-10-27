using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.Modules.Promotion.Contracts;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Đổi trạng thái đơn kèm tác động tồn kho (BR-11): đóng gói → trừ tồn FIFO, hủy → hoàn tạm giữ và trả lượt dùng
/// khuyến mãi, hoàn hàng → nhập lại kho.
/// </summary>
/// <remarks>
/// Đơn và tồn kho nằm ở hai DbContext nên không chung transaction. Dòng đơn bị khóa (FOR UPDATE) suốt
/// quá trình để hai thao tác trên cùng đơn không xen nhau; các thao tác Inventory idempotent theo mã đơn
/// nên gọi lại sau lỗi vẫn an toàn.
/// </remarks>
internal sealed class OrderStatusService(
    OrderingDbContext dbContext,
    IInventoryService inventoryService,
    IPromotionService promotionService,
    OrderNotificationOutbox notificationOutbox,
    TimeProvider timeProvider,
    ILogger<OrderStatusService> logger)
{
    // Ghi vào lịch sử các bước trung gian khi chuyển nhanh; ghi chú của nhân viên gắn vào bước cuối.
    private const string FastForwardNote = "Chuyển nhanh nhiều bước";

    /// <param name="staffNotification">Báo nhân viên qua outbox, lưu cùng transaction với trạng thái mới.</param>
    public Task<Order> ChangeAsync(
        long orderId,
        OrderStatus target,
        long? changedBy,
        string? note,
        Action<Order>? guard,
        OrderNotificationKind? staffNotification,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            orderId,
            order =>
            {
                guard?.Invoke(order);
                return [target];
            },
            changedBy,
            note,
            staffNotification,
            cancellationToken);

    /// <summary>
    /// Chuyển tới <paramref name="target"/>: một bước nếu chuyển thẳng được, hoặc đi lần lượt các bước của luồng chính
    /// (<see cref="OrderStatusMachine.PathTo"/>) trong cùng một transaction, mỗi bước vẫn chạy đủ tác động kho và ghi
    /// lịch sử. Lỗi ở bước nào (ví dụ thiếu tồn khi đóng gói) thì cả lần chuyển không được lưu.
    /// </summary>
    public Task<Order> AdvanceAsync(
        long orderId,
        OrderStatus target,
        long? changedBy,
        string? note,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            orderId,
            // Tính đường đi sau khi đã khóa dòng đơn nên luôn theo trạng thái mới nhất.
            order => OrderStatusMachine.PathTo(order.Status, target)
                ?? throw OrderErrors.InvalidTransition(order.Status, target),
            changedBy,
            note,
            staffNotification: null,
            cancellationToken);

    private async Task<Order> ApplyAsync(
        long orderId,
        Func<Order, IReadOnlyList<OrderStatus>> planSteps,
        long? changedBy,
        string? note,
        OrderNotificationKind? staffNotification,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM ordering.orders WHERE id = {orderId} FOR UPDATE", cancellationToken);
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusLogs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw OrderErrors.NotFound();

        var initial = order.Status;
        var steps = planSteps(order);
        for (var i = 0; i < steps.Count; i++)
        {
            var isLast = i == steps.Count - 1;
            await ApplyStepAsync(order, steps[i], changedBy, isLast ? note : FastForwardNote, cancellationToken);
        }

        if (staffNotification is { } kind)
        {
            notificationOutbox.Enqueue(order, kind);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Order {OrderCode} changed from {FromStatus} to {ToStatus} in {StepCount} step(s) by {UserId}",
            order.OrderCode, initial, order.Status, steps.Count, changedBy);
        return order;
    }

    private async Task ApplyStepAsync(
        Order order,
        OrderStatus target,
        long? changedBy,
        string? note,
        CancellationToken cancellationToken)
    {
        var previous = order.Status;
        order.ChangeStatus(target, changedBy, note, timeProvider.GetUtcNow().UtcDateTime);

        switch (target)
        {
            case OrderStatus.Packing:
                order.SetCostPrices(await inventoryService.DeductAsync(order.OrderCode, cancellationToken));
                break;
            case OrderStatus.Cancelled:
                await inventoryService.ReleaseAsync(order.OrderCode, cancellationToken);
                await promotionService.ReleaseAsync(order.OrderCode, cancellationToken);
                break;
            case OrderStatus.Returned when OrderStatusMachine.IsStockDeducted(previous):
                await inventoryService.RestockAsync(order.OrderCode, cancellationToken);
                break;
        }
    }
}

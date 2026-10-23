using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Đổi trạng thái đơn kèm tác động tồn kho (BR-11): đóng gói → trừ tồn FIFO, hủy → hoàn tạm giữ,
/// hoàn hàng → nhập lại kho.
/// </summary>
/// <remarks>
/// Đơn và tồn kho nằm ở hai DbContext nên không chung transaction. Dòng đơn bị khóa (FOR UPDATE) suốt
/// quá trình để hai thao tác trên cùng đơn không xen nhau; các thao tác Inventory idempotent theo mã đơn
/// nên gọi lại sau lỗi vẫn an toàn.
/// </remarks>
internal sealed class OrderStatusService(
    OrderingDbContext dbContext,
    IInventoryService inventoryService,
    TimeProvider timeProvider,
    ILogger<OrderStatusService> logger)
{
    public async Task<Order> ChangeAsync(
        long orderId,
        OrderStatus target,
        long? changedBy,
        string? note,
        Action<Order>? guard,
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

        guard?.Invoke(order);
        var previous = order.Status;
        order.ChangeStatus(target, changedBy, note, timeProvider.GetUtcNow().UtcDateTime);

        switch (target)
        {
            case OrderStatus.Packing:
                order.SetCostPrices(await inventoryService.DeductAsync(order.OrderCode, cancellationToken));
                break;
            case OrderStatus.Cancelled:
                await inventoryService.ReleaseAsync(order.OrderCode, cancellationToken);
                break;
            case OrderStatus.Returned when OrderStatusMachine.IsStockDeducted(previous):
                await inventoryService.RestockAsync(order.OrderCode, cancellationToken);
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Order {OrderCode} changed from {FromStatus} to {ToStatus} by {UserId}",
            order.OrderCode, previous, target, changedBy);
        return order;
    }
}

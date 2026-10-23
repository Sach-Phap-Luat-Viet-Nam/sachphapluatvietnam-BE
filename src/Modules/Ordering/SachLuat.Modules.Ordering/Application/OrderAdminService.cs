using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>Quản lý đơn hàng trên Admin (FR-70).</summary>
internal sealed class OrderAdminService(OrderingDbContext dbContext, OrderStatusService statusService)
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<PagedResponse<OrderSummaryResponse>> ListAsync(
        AdminOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var orders = dbContext.Orders.AsNoTracking();
        if (SnakeCaseEnumConverter<OrderStatus>.TryParse(query.Status, out var status))
        {
            orders = orders.Where(order => order.Status == status);
        }

        if (SnakeCaseEnumConverter<PaymentMethod>.TryParse(query.PaymentMethod, out var paymentMethod))
        {
            orders = orders.Where(order => order.PaymentMethod == paymentMethod);
        }

        if (query.From is { } from)
        {
            var fromUtc = StartOfVietnamDayUtc(from);
            orders = orders.Where(order => order.CreatedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = StartOfVietnamDayUtc(to.AddDays(1));
            orders = orders.Where(order => order.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            orders = orders.Where(order => order.OrderCode == term || order.Phone == term);
        }

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(order => new OrderSummaryResponse(
                order.OrderCode,
                order.Status,
                order.PaymentMethod,
                order.PaymentStatus,
                order.CustomerName,
                order.Phone,
                order.Items.Sum(item => item.Quantity),
                order.Total,
                order.CreatedAt))
            .ToListAsync(cancellationToken);
        return PagedResponse<OrderSummaryResponse>.Create(items, query, total);
    }

    public async Task<AdminOrderResponse> GetAsync(string orderCode, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusLogs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.OrderCode == orderCode, cancellationToken)
            ?? throw OrderErrors.NotFound();
        return order.ToAdminResponse();
    }

    public async Task<AdminOrderResponse> ChangeStatusAsync(
        long staffUserId,
        string orderCode,
        ChangeOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var orderId = await dbContext.Orders.AsNoTracking()
            .Where(order => order.OrderCode == orderCode)
            .Select(order => (long?)order.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw OrderErrors.NotFound();

        var order = await statusService.ChangeAsync(
            orderId, request.Status, staffUserId, request.Note?.Trim(), guard: null, cancellationToken);
        return order.ToAdminResponse();
    }

    private static DateTime StartOfVietnamDayUtc(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
}

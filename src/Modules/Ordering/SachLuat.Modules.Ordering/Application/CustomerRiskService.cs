using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>Một đơn cũ của số điện thoại, chỉ gồm các dữ kiện cần để đánh giá rủi ro.</summary>
/// <param name="WasDelivered">Đơn từng sang <c>delivered</c> (khách đã nhận hàng).</param>
/// <param name="WasReturnedInTransit">Đơn từng sang <c>returning</c>: giao thất bại, hàng quay về kho.</param>
internal sealed record CustomerOrderFacts(
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    bool WasDelivered,
    bool WasReturnedInTransit);

/// <summary>
/// Chống đặt nhầm và "bom hàng" COD theo số điện thoại (mọi đơn, cả thành viên lẫn khách vãng lai): giới hạn số
/// đơn COD đang mở, khóa COD khi giao thất bại nhiều lần; CSKH xem cùng số liệu ở Admin.
/// </summary>
internal sealed class CustomerRiskService(OrderingDbContext dbContext, IOptions<OrderingOptions> options)
{
    // Đơn COD đã tạo nhưng chưa tới tay khách và chưa kết thúc.
    private static readonly OrderStatus[] OpenStatuses =
    [
        OrderStatus.PendingConfirm,
        OrderStatus.Confirmed,
        OrderStatus.Packing,
        OrderStatus.ReadyToShip,
        OrderStatus.Shipping,
    ];

    private readonly OrderingOptions _options = options.Value;

    public async Task<CustomerRiskResponse> GetAsync(string phone, CancellationToken cancellationToken)
    {
        var normalized = PhoneNumbers.Normalize(phone);
        var orders = await dbContext.Orders.AsNoTracking()
            .Where(order => order.Phone == normalized)
            .Select(order => new CustomerOrderFacts(
                order.Status,
                order.PaymentMethod,
                order.StatusLogs.Any(log => log.ToStatus == OrderStatus.Delivered),
                order.StatusLogs.Any(log => log.ToStatus == OrderStatus.Returning)))
            .ToListAsync(cancellationToken);
        return Evaluate(orders, _options);
    }

    /// <summary>Ném lỗi nếu số điện thoại không được đặt thêm đơn COD.</summary>
    public async Task EnsureCodAllowedAsync(string phone, CancellationToken cancellationToken)
    {
        var risk = await GetAsync(phone, cancellationToken);
        if (risk.CodBlocked)
        {
            throw OrderErrors.CodNotAllowed();
        }

        if (risk.OpenCodLimitReached)
        {
            throw OrderErrors.TooManyOpenOrders(_options.MaxOpenCodOrdersPerPhone);
        }
    }

    public static CustomerRiskResponse Evaluate(
        IReadOnlyCollection<CustomerOrderFacts> orders,
        OrderingOptions options)
    {
        var cod = orders.Where(order => order.PaymentMethod == PaymentMethod.Cod).ToList();

        // Chỉ tính giao thất bại (shipping → returning); đổi trả sau khi đã nhận (return_requested) không phải bom hàng.
        var failedCod = cod.Count(order => order.WasReturnedInTransit);
        var openCod = cod.Count(order => OpenStatuses.Contains(order.Status));
        return new CustomerRiskResponse(
            orders.Count,
            orders.Count(order => order.WasDelivered),
            orders.Count(order => order.Status == OrderStatus.Cancelled),
            failedCod,
            openCod,
            options.CodFailedDeliveryLimit > 0 && failedCod >= options.CodFailedDeliveryLimit,
            options.MaxOpenCodOrdersPerPhone > 0 && openCod >= options.MaxOpenCodOrdersPerPhone);
    }
}

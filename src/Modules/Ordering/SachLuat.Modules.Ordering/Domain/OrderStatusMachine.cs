namespace SachLuat.Modules.Ordering.Domain;

/// <summary>Bảng chuyển trạng thái đơn hàng, đúng cột "Chuyển tiếp được sang" ở mục 16.1.</summary>
internal static class OrderStatusMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.PendingConfirm] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.PreorderWaiting] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Confirmed],
        [OrderStatus.Confirmed] = [OrderStatus.Packing, OrderStatus.Cancelled],
        [OrderStatus.Packing] = [OrderStatus.ReadyToShip],
        [OrderStatus.ReadyToShip] = [OrderStatus.Shipping],
        [OrderStatus.Shipping] = [OrderStatus.Delivered, OrderStatus.Returning],
        [OrderStatus.Delivered] = [OrderStatus.Completed, OrderStatus.ReturnRequested],
        [OrderStatus.ReturnRequested] = [OrderStatus.Returned, OrderStatus.Delivered],
        [OrderStatus.Returning] = [OrderStatus.Returned],
        [OrderStatus.Returned] = [OrderStatus.Refunded],
        [OrderStatus.Completed] = [],
        [OrderStatus.Refunded] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) =>
        Transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Đã trừ tồn thật (từ bước đóng gói trở đi) thì hoàn hàng phải nhập lại kho.</summary>
    public static bool IsStockDeducted(OrderStatus status) => status is OrderStatus.Packing
        or OrderStatus.ReadyToShip
        or OrderStatus.Shipping
        or OrderStatus.Delivered
        or OrderStatus.Completed
        or OrderStatus.ReturnRequested
        or OrderStatus.Returning;
}

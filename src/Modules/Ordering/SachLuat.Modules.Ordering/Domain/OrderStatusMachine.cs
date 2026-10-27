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

    /// <summary>
    /// Luồng chính của đơn suôn sẻ, dùng cho chuyển nhanh nhiều bước. Không gồm bước rẽ nhánh hay khó quay lại (hủy,
    /// hoàn, xác nhận thanh toán) và dừng ở <c>delivered</c> vì sau khi giao còn thời gian khách yêu cầu đổi trả.
    /// </summary>
    private static readonly Dictionary<OrderStatus, OrderStatus> MainFlowNext = new()
    {
        [OrderStatus.PendingConfirm] = OrderStatus.Confirmed,
        [OrderStatus.PreorderWaiting] = OrderStatus.Confirmed,
        [OrderStatus.Paid] = OrderStatus.Confirmed,
        [OrderStatus.Confirmed] = OrderStatus.Packing,
        [OrderStatus.Packing] = OrderStatus.ReadyToShip,
        [OrderStatus.ReadyToShip] = OrderStatus.Shipping,
        [OrderStatus.Shipping] = OrderStatus.Delivered,
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) =>
        Transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>
    /// Các bước đi từ <paramref name="from"/> tới <paramref name="to"/>: một bước nếu chuyển thẳng được, nhiều bước nếu
    /// <paramref name="to"/> nằm phía sau trên luồng chính; null nếu không tới được.
    /// </summary>
    public static IReadOnlyList<OrderStatus>? PathTo(OrderStatus from, OrderStatus to)
    {
        if (CanTransition(from, to))
        {
            return [to];
        }

        var path = new List<OrderStatus>();
        var current = from;
        while (MainFlowNext.TryGetValue(current, out var next))
        {
            path.Add(next);
            if (next == to)
            {
                return path;
            }

            current = next;
        }

        return null;
    }

    /// <summary>Trạng thái chuyển nhanh được (cách từ hai bước trở lên trên luồng chính), kèm các bước sẽ đi qua.</summary>
    public static IReadOnlyList<IReadOnlyList<OrderStatus>> FastForwardPaths(OrderStatus from)
    {
        var paths = new List<IReadOnlyList<OrderStatus>>();
        var path = new List<OrderStatus>();
        var current = from;
        while (MainFlowNext.TryGetValue(current, out var next))
        {
            path.Add(next);
            if (path.Count >= 2)
            {
                paths.Add([.. path]);
            }

            current = next;
        }

        return paths;
    }

    /// <summary>Đã trừ tồn thật (từ bước đóng gói trở đi) thì hoàn hàng phải nhập lại kho.</summary>
    public static bool IsStockDeducted(OrderStatus status) => status is OrderStatus.Packing
        or OrderStatus.ReadyToShip
        or OrderStatus.Shipping
        or OrderStatus.Delivered
        or OrderStatus.Completed
        or OrderStatus.ReturnRequested
        or OrderStatus.Returning;
}

using AwesomeAssertions;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class OrderStatusMachineTests
{
    [Fact]
    public void PathTo_DirectTransition_IsSingleStep()
    {
        OrderStatusMachine.PathTo(OrderStatus.PendingConfirm, OrderStatus.Cancelled)
            .Should().Equal(OrderStatus.Cancelled);
        OrderStatusMachine.PathTo(OrderStatus.Shipping, OrderStatus.Returning)
            .Should().Equal(OrderStatus.Returning);
    }

    [Fact]
    public void PathTo_LaterMainFlowStatus_ListsEveryStep()
    {
        OrderStatusMachine.PathTo(OrderStatus.PendingConfirm, OrderStatus.ReadyToShip)
            .Should().Equal(OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.ReadyToShip);
        OrderStatusMachine.PathTo(OrderStatus.Paid, OrderStatus.Delivered).Should().Equal(
            OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.ReadyToShip, OrderStatus.Shipping,
            OrderStatus.Delivered);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.PendingPayment), nameof(OrderStatus.Confirmed))]
    [InlineData(nameof(OrderStatus.PendingConfirm), nameof(OrderStatus.Completed))]
    [InlineData(nameof(OrderStatus.Confirmed), nameof(OrderStatus.Returned))]
    [InlineData(nameof(OrderStatus.Packing), nameof(OrderStatus.Confirmed))]
    [InlineData(nameof(OrderStatus.Delivered), nameof(OrderStatus.Refunded))]
    [InlineData(nameof(OrderStatus.Cancelled), nameof(OrderStatus.Confirmed))]
    public void PathTo_NotReachable_ReturnsNull(string from, string to)
    {
        OrderStatusMachine.PathTo(Enum.Parse<OrderStatus>(from), Enum.Parse<OrderStatus>(to)).Should().BeNull();
    }

    [Fact]
    public void FastForwardPaths_FromPendingConfirm_SkipsImmediateNextAndStopsAtDelivered()
    {
        var paths = OrderStatusMachine.FastForwardPaths(OrderStatus.PendingConfirm);

        paths.Select(path => path[^1]).Should().Equal(
            OrderStatus.Packing, OrderStatus.ReadyToShip, OrderStatus.Shipping, OrderStatus.Delivered);
        paths[0].Should().Equal(OrderStatus.Confirmed, OrderStatus.Packing);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.PendingPayment))]
    [InlineData(nameof(OrderStatus.Shipping))]
    [InlineData(nameof(OrderStatus.Delivered))]
    [InlineData(nameof(OrderStatus.Returning))]
    [InlineData(nameof(OrderStatus.Cancelled))]
    public void FastForwardPaths_NoTwoStepMainFlowAhead_IsEmpty(string from)
    {
        OrderStatusMachine.FastForwardPaths(Enum.Parse<OrderStatus>(from)).Should().BeEmpty();
    }
}

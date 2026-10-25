using AwesomeAssertions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class CustomerRiskServiceTests
{
    private static readonly OrderingOptions Options = new()
    {
        MaxOpenCodOrdersPerPhone = 3,
        CodFailedDeliveryLimit = 2,
    };

    [Fact]
    public void Evaluate_NoOrders_AllowsCod()
    {
        var risk = CustomerRiskService.Evaluate([], Options);

        risk.TotalOrders.Should().Be(0);
        risk.CodAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CountsDeliveredCancelledAndOpenCod()
    {
        var risk = CustomerRiskService.Evaluate(
            [
                Cod(OrderStatus.PendingConfirm),
                Cod(OrderStatus.Shipping),
                Cod(OrderStatus.Completed, delivered: true),
                Cod(OrderStatus.Cancelled),
                new CustomerOrderFacts(OrderStatus.Confirmed, PaymentMethod.BankTransfer, false, false),
            ],
            Options);

        risk.TotalOrders.Should().Be(5);
        risk.DeliveredCount.Should().Be(1);
        risk.CancelledCount.Should().Be(1);
        risk.OpenCodCount.Should().Be(2, "đơn chuyển khoản không tính vào giới hạn COD");
        risk.CodAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_OpenCodAtLimit_BlocksNewCod()
    {
        var risk = CustomerRiskService.Evaluate(
            [Cod(OrderStatus.PendingConfirm), Cod(OrderStatus.Confirmed), Cod(OrderStatus.Packing)], Options);

        risk.OpenCodLimitReached.Should().BeTrue();
        risk.CodBlocked.Should().BeFalse();
        risk.CodAllowed.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_FailedDeliveriesAtLimit_BlocksCod()
    {
        var risk = CustomerRiskService.Evaluate(
            [Cod(OrderStatus.Returned, returnedInTransit: true), Cod(OrderStatus.Returning, returnedInTransit: true)],
            Options);

        risk.FailedCodCount.Should().Be(2);
        risk.CodBlocked.Should().BeTrue();
        risk.CodAllowed.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnAfterDelivery_IsNotFailedDelivery()
    {
        var risk = CustomerRiskService.Evaluate(
            [Cod(OrderStatus.Returned, delivered: true), Cod(OrderStatus.Refunded, delivered: true)], Options);

        risk.FailedCodCount.Should().Be(0);
        risk.CodAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LimitsZero_NeverBlocks()
    {
        var risk = CustomerRiskService.Evaluate(
            [
                Cod(OrderStatus.PendingConfirm),
                Cod(OrderStatus.Returned, returnedInTransit: true),
                Cod(OrderStatus.Returned, returnedInTransit: true),
            ],
            new OrderingOptions { MaxOpenCodOrdersPerPhone = 0, CodFailedDeliveryLimit = 0 });

        risk.CodAllowed.Should().BeTrue();
    }

    private static CustomerOrderFacts Cod(
        OrderStatus status,
        bool delivered = false,
        bool returnedInTransit = false) => new(status, PaymentMethod.Cod, delivered, returnedInTransit);
}

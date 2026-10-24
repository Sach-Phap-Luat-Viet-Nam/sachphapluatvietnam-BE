using AwesomeAssertions;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Cod_StartsPendingConfirmWithoutPaymentDeadline()
    {
        var order = CreateOrder(PaymentMethod.Cod);

        order.Status.Should().Be(OrderStatus.PendingConfirm);
        order.PaymentExpiresAt.Should().BeNull();
        order.Subtotal.Should().Be(450_000m);
        order.Total.Should().Be(480_000m);
        order.StatusLogs.Should().ContainSingle(log => log.FromStatus == null && log.ToStatus == OrderStatus.PendingConfirm);
    }

    [Fact]
    public void Create_BankTransfer_StartsPendingPaymentWithDeadline()
    {
        var order = CreateOrder(PaymentMethod.BankTransfer);

        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.PaymentExpiresAt.Should().Be(Now.AddMinutes(30));
    }

    [Fact]
    public void ChangeStatus_ToPaid_MarksPaymentPaidAndClearsDeadline()
    {
        var order = CreateOrder(PaymentMethod.BankTransfer);

        order.ChangeStatus(OrderStatus.Paid, changedBy: 9, note: "Đã nhận chuyển khoản", Now);

        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.PaymentExpiresAt.Should().BeNull();
        order.StatusLogs.Last().Should().Match<OrderStatusLog>(log =>
            log.FromStatus == OrderStatus.PendingPayment && log.ToStatus == OrderStatus.Paid && log.ChangedBy == 9);
    }

    [Fact]
    public void ChangeStatus_NotInTransitionTable_Throws()
    {
        var order = CreateOrder(PaymentMethod.Cod);

        var act = () => order.ChangeStatus(OrderStatus.Shipping, null, null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(OrderErrors.InvalidTransitionCode);
    }

    [Fact]
    public void ChangeStatus_CodDelivered_MarksPaymentPaid()
    {
        var order = CreateOrder(PaymentMethod.Cod);
        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.ReadyToShip, OrderStatus.Shipping,
                     OrderStatus.Delivered,
                 })
        {
            order.ChangeStatus(status, null, null, Now);
        }

        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void ChangeStatus_RefundUnpaidOrder_Throws()
    {
        var order = CreateOrder(PaymentMethod.Cod);
        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.ReadyToShip, OrderStatus.Shipping,
                     OrderStatus.Returning, OrderStatus.Returned,
                 })
        {
            order.ChangeStatus(status, null, null, Now);
        }

        var act = () => order.ChangeStatus(OrderStatus.Refunded, null, null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(OrderErrors.OrderNotPaidCode);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.PendingPayment), true)]
    [InlineData(nameof(OrderStatus.PendingConfirm), true)]
    [InlineData(nameof(OrderStatus.Confirmed), true)]
    [InlineData(nameof(OrderStatus.Paid), false)]
    [InlineData(nameof(OrderStatus.Packing), false)]
    [InlineData(nameof(OrderStatus.Shipping), false)]
    public void StatusMachine_CancelAllowedOnlyBeforePacking(string from, bool expected)
    {
        OrderStatusMachine.CanTransition(Enum.Parse<OrderStatus>(from), OrderStatus.Cancelled).Should().Be(expected);
    }

    [Fact]
    public void SetCostPrices_FillsMatchingItems()
    {
        var order = CreateOrder(PaymentMethod.Cod);

        order.SetCostPrices(new Dictionary<long, decimal> { [1] = 80_000m });

        order.Items.Single(item => item.SkuId == 1).CostPrice.Should().Be(80_000m);
        order.Items.Single(item => item.SkuId == 2).CostPrice.Should().BeNull();
    }

    [Fact]
    public void Create_WithPromotions_SubtractsDiscountAndShippingDiscount()
    {
        // 450.000 - 90.000 (20% tổng đơn, chia theo dòng) + 30.000 phí ship - 30.000 miễn phí ship = 360.000.
        var order = CreateOrder(
            PaymentMethod.Cod,
            new OrderCharges(
                90_000m,
                ShippingFee: 30_000m,
                ShippingBaseFee: 30_000m,
                ShippingDiscount: 30_000m,
                [new AppliedPromotionSnapshot(1, "Giảm 20%", "order_discount", null, 90_000m)]),
            itemDiscounts: [60_000m, 30_000m]);

        order.Subtotal.Should().Be(450_000m);
        order.Discount.Should().Be(90_000m);
        order.ShippingDiscount.Should().Be(30_000m);
        order.Total.Should().Be(360_000m);
        order.Items.Select(item => item.DiscountAmount).Should().Equal(60_000m, 30_000m);
        order.AppliedPromotions.Should().ContainSingle(promotion => promotion.Amount == 90_000m);
    }

    [Fact]
    public void Create_FreeShippingByThreshold_KeepsBaseFeeForReporting()
    {
        var order = CreateOrder(
            PaymentMethod.Cod, new OrderCharges(0m, ShippingFee: 0m, ShippingBaseFee: 30_000m, 0m, []));

        order.ShippingFee.Should().Be(0m);
        order.ShippingBaseFee.Should().Be(30_000m);
        order.Total.Should().Be(450_000m);
    }

    [Fact]
    public void Create_DiscountNotMatchingItemDiscounts_Throws()
    {
        var act = () => CreateOrder(
            PaymentMethod.Cod, new OrderCharges(90_000m, 30_000m, 30_000m, 0m, []), itemDiscounts: [60_000m, 0m]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShippingDiscountAboveShippingFee_Throws()
    {
        var act = () => CreateOrder(PaymentMethod.Cod, new OrderCharges(0m, 30_000m, 30_000m, 40_000m, []));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyDiscount_AboveLineTotal_Throws()
    {
        var item = new OrderItem(1, "SLVN-A-BM", "Bộ luật Dân sự", null, "9780306406157", "paperback", 150_000m, 1);

        var act = () => item.ApplyDiscount(150_001m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Order CreateOrder(
        PaymentMethod paymentMethod,
        OrderCharges? charges = null,
        decimal[]? itemDiscounts = null)
    {
        var items = new[]
        {
            new OrderItem(1, "SLVN-A-BM", "Bộ luật Dân sự", null, "9780306406157", "paperback", 150_000m, 2),
            new OrderItem(2, "SLVN-B-BM", "Luật Đất đai", null, "9786041234567", "paperback", 150_000m, 1),
        };
        for (var index = 0; index < (itemDiscounts?.Length ?? 0); index++)
        {
            items[index].ApplyDiscount(itemDiscounts![index]);
        }

        return Order.Create(
            "SLVN2610230001",
            userId: null,
            "Nguyễn Văn A",
            "0901234567",
            email: null,
            new ShippingAddress("Hà Nội", "Phường Cầu Giấy", "1 Xuân Thủy"),
            paymentMethod,
            invoiceInfo: null,
            note: null,
            idempotencyKey: null,
            items,
            charges ?? OrderCharges.Shipping(30_000m),
            Now,
            TimeSpan.FromMinutes(30));
    }
}

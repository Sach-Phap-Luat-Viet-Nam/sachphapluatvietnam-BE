using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class OrderNotificationTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_IsPendingAndDueImmediately()
    {
        var notification = new OrderNotification(CreateOrder(), OrderNotificationKind.NewOrder, Now);

        notification.Status.Should().Be(OrderNotificationStatus.Pending);
        notification.NextAttemptAt.Should().Be(Now);
        notification.Attempts.Should().Be(0);
    }

    [Fact]
    public void MarkSent_ClearsRetryState()
    {
        var notification = new OrderNotification(CreateOrder(), OrderNotificationKind.NewOrder, Now);
        notification.MarkFailed("timeout", Now, maxAttempts: 5);

        notification.MarkSent(Now.AddMinutes(1));

        notification.Status.Should().Be(OrderNotificationStatus.Sent);
        notification.SentAt.Should().Be(Now.AddMinutes(1));
        notification.NextAttemptAt.Should().BeNull();
        notification.LastError.Should().BeNull();
        notification.Attempts.Should().Be(2);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(8, 3600)]
    [InlineData(9, 3600)]
    public void MarkFailed_BacksOffExponentiallyUpToOneHour(int failures, int expectedDelaySeconds)
    {
        var notification = new OrderNotification(CreateOrder(), OrderNotificationKind.Paid, Now);

        for (var i = 0; i < failures; i++)
        {
            notification.MarkFailed("Zalo lỗi", Now, maxAttempts: 20);
        }

        notification.Status.Should().Be(OrderNotificationStatus.Pending);
        notification.NextAttemptAt.Should().Be(Now.AddSeconds(expectedDelaySeconds));
        notification.LastError.Should().Be("Zalo lỗi");
    }

    [Fact]
    public void MarkFailed_ReachingMaxAttempts_GivesUp()
    {
        var notification = new OrderNotification(CreateOrder(), OrderNotificationKind.Paid, Now);

        notification.MarkFailed("lỗi 1", Now, maxAttempts: 2);
        notification.MarkFailed(new string('x', 600), Now, maxAttempts: 2);

        notification.Status.Should().Be(OrderNotificationStatus.Failed);
        notification.NextAttemptAt.Should().BeNull();
        notification.LastError.Should().HaveLength(500);
    }

    [Fact]
    public void Claim_PushesNextAttemptByLease()
    {
        var notification = new OrderNotification(CreateOrder(), OrderNotificationKind.NewOrder, Now);

        notification.Claim(Now, TimeSpan.FromMinutes(5));

        notification.NextAttemptAt.Should().Be(Now.AddMinutes(5));
        notification.Status.Should().Be(OrderNotificationStatus.Pending);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Outbox_EnqueuesOnlyWhenNotifierEnabled(bool enabled, int expected)
    {
        using var dbContext = new OrderingDbContext(new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);
        var notifier = Substitute.For<IStaffNotifier>();
        notifier.IsEnabled.Returns(enabled);
        var outbox = new OrderNotificationOutbox(dbContext, notifier, new FakeTimeProvider(Now));

        outbox.Enqueue(CreateOrder(), OrderNotificationKind.NewOrder);

        dbContext.ChangeTracker.Entries<OrderNotification>().Should().HaveCount(expected);
    }

    internal static Order CreateOrder(
        PaymentMethod paymentMethod = PaymentMethod.Cod,
        string customerName = "Nguyễn Văn An")
    {
        var items = new[]
        {
            new OrderItem(1, "SLVN-A-BM", "Bộ luật Dân sự", null, "9780306406157", "paperback", 150_000m, 2),
            new OrderItem(2, "SLVN-B-BM", "Luật Đất đai", null, "9786041234567", "paperback", 150_000m, 1),
        };
        return Order.Create(
            "SLVN2610270001",
            userId: null,
            customerName,
            "0912345678",
            email: null,
            new ShippingAddress("Hà Nội", "Phường Cầu Giấy", "1 Xuân Thủy"),
            paymentMethod,
            invoiceInfo: null,
            note: null,
            idempotencyKey: null,
            items,
            OrderCharges.Shipping(30_000m),
            Now,
            TimeSpan.FromMinutes(30));
    }
}

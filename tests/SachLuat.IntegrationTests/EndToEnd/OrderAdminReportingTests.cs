using System.IO.Compression;
using System.Xml.Linq;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

[Collection(EndToEndCollection.Name)]
public sealed class OrderAdminReportingTests(EndToEndFactory factory)
{
    [Fact]
    public async Task StatisticsAndExport_UseAllFilteredOrdersAndVietnamDateBoundaries()
    {
        factory.SkipIfNotConfigured();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<OrderAdminService>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var phone = "0987650012";
        var dayStart = new DateTime(2026, 11, 11, 17, 0, 0, DateTimeKind.Utc);
        var delivered = CreateOrder(phone, PaymentMethod.Cod, OrderStatus.Delivered, 3);
        var completed = CreateOrder(phone, PaymentMethod.BankTransfer, OrderStatus.Completed, 2);
        var pending = CreateOrder(phone, PaymentMethod.Cod, OrderStatus.PendingConfirm, 10);
        var returned = CreateOrder(phone, PaymentMethod.Cod, OrderStatus.Returned, 4);
        var before = CreateOrder(phone, PaymentMethod.Cod, OrderStatus.Delivered, 5);
        var after = CreateOrder(phone, PaymentMethod.Cod, OrderStatus.Delivered, 6);
        db.Orders.AddRange(delivered, completed, pending, returned, before, after);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var order in new[] { delivered, completed, pending, returned })
        {
            db.Entry(order).Property(value => value.CreatedAt).CurrentValue = dayStart;
        }

        db.Entry(before).Property(value => value.CreatedAt).CurrentValue = dayStart.AddTicks(-10);
        db.Entry(after).Property(value => value.CreatedAt).CurrentValue = dayStart.AddDays(1);
        await db.SaveChangesAsync(cancellationToken);

        var query = new AdminOrdersQuery
        {
            Q = "+84987650012",
            From = new DateOnly(2026, 11, 12),
            To = new DateOnly(2026, 11, 12),
            Page = 1,
            Limit = 1,
        };
        var result = await service.ListAsync(query, cancellationToken);
        result.Items.Should().ContainSingle();
        result.Meta.Total.Should().Be(4);
        result.Statistics.Should().Be(new OrderStatisticsResponse(450_000m, 5));
        query.Page = 10;
        var beyondPage = await service.ListAsync(query, cancellationToken);
        beyondPage.Items.Should().BeEmpty();
        beyondPage.Statistics.Should().Be(result.Statistics);

        using var stream = new MemoryStream(await service.ExportAsync(query, cancellationToken));
        using var archive = new ZipArchive(stream);
        var entry = archive.GetEntry("xl/worksheets/sheet2.xml")
            ?? throw new InvalidOperationException("Missing orders worksheet");
        using var sheet = entry.Open();
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument.Load(sheet).Descendants(spreadsheet + "row").Should().HaveCount(5);

        query.Page = 1;
        query.PaymentMethod = "bank_transfer";
        (await service.ListAsync(query, cancellationToken)).Statistics
            .Should().Be(new OrderStatisticsResponse(180_000m, 2));
        query.PaymentMethod = null;
        query.Status = "pending_confirm";
        var unsold = await service.ListAsync(query, cancellationToken);
        unsold.Meta.Total.Should().Be(1);
        unsold.Statistics.Should().Be(new OrderStatisticsResponse(0m, 0));
        query.Status = null;
        query.Q = delivered.OrderCode;
        (await service.ListAsync(query, cancellationToken)).Statistics
            .Should().Be(new OrderStatisticsResponse(270_000m, 3));
        query.Q = "NO-MATCH";
        var empty = await service.ListAsync(query, cancellationToken);
        empty.Meta.Total.Should().Be(0);
        empty.Statistics.Should().Be(new OrderStatisticsResponse(0m, 0));
    }

    private static Order CreateOrder(string phone, PaymentMethod method, OrderStatus target, int quantity)
    {
        var item = new OrderItem(1, "REPORT-SKU", "Sách báo cáo", null, "0123456789012", "paperback", 100_000m,
            quantity);
        item.ApplyDiscount(quantity * 10_000m);
        var order = Order.Create(Guid.NewGuid().ToString("N")[..20], null, "Khách báo cáo", phone, null,
            new ShippingAddress("Hà Nội", "Phường", "Đường"), method, null, null, null, [item],
            new OrderCharges(item.DiscountAmount, 30_000m, 30_000m, 5_000m, []), DateTime.UtcNow,
            TimeSpan.FromMinutes(30));
        if (method == PaymentMethod.BankTransfer)
        {
            order.ChangeStatus(OrderStatus.Paid, null, null, DateTime.UtcNow);
        }

        var mainTarget = target is OrderStatus.Returned or OrderStatus.Completed ? OrderStatus.Delivered : target;
        foreach (var status in OrderStatusMachine.PathTo(order.Status, mainTarget) ?? [])
        {
            order.ChangeStatus(status, null, null, DateTime.UtcNow);
        }

        if (target == OrderStatus.Completed)
        {
            order.ChangeStatus(target, null, null, DateTime.UtcNow);
        }
        else if (target == OrderStatus.Returned)
        {
            order.ChangeStatus(OrderStatus.ReturnRequested, null, null, DateTime.UtcNow);
            order.ChangeStatus(target, null, null, DateTime.UtcNow);
        }

        return order;
    }
}

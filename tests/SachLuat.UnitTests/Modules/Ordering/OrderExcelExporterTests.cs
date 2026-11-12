using System.IO.Compression;
using System.Xml.Linq;
using AwesomeAssertions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class OrderExcelExporterTests
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Export_PreservesTextAndNumbersAndCountsOnlySuccessfulSales()
    {
        var delivered = CreateOrder("SALE", 3, delivered: true);
        var pending = CreateOrder("PENDING", 10, delivered: false);
        var completed = CreateOrder("DONE", 2, delivered: true);
        completed.ChangeStatus(OrderStatus.Completed, null, null, DateTime.UtcNow);
        var returned = CreateOrder("RETURN", 5, delivered: true);
        returned.ChangeStatus(OrderStatus.ReturnRequested, null, null, DateTime.UtcNow);
        returned.ChangeStatus(OrderStatus.Returned, null, null, DateTime.UtcNow);

        var bytes = OrderExcelExporter.Export([delivered, pending, completed, returned], new AdminOrdersQuery(),
            TestContext.Current.CancellationToken);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream);
        var workbook = Read(archive, "xl/workbook.xml");
        workbook.Descendants(Spreadsheet + "sheet").Select(sheet => (string?)sheet.Attribute("name"))
            .Should().Equal("Thống kê", "Đơn hàng", "Chi tiết sách");
        var statistics = Read(archive, "xl/worksheets/sheet1.xml");
        var rows = statistics.Descendants(Spreadsheet + "row").ToList();
        rows[1].Descendants(Spreadsheet + "v").Single().Value.Should().Be("4");
        rows[2].Descendants(Spreadsheet + "v").Single().Value.Should().Be("450000");
        rows[3].Descendants(Spreadsheet + "v").Single().Value.Should().Be("5");

        var orderSheet = Read(archive, "xl/worksheets/sheet2.xml");
        orderSheet.Descendants(Spreadsheet + "row").Should().HaveCount(5);
        var phone = orderSheet.Descendants(Spreadsheet + "c")
            .First(cell => cell.Value == "0901234567");
        ((string?)phone.Attribute("t")).Should().Be("inlineStr");
        var itemSheet = Read(archive, "xl/worksheets/sheet3.xml");
        itemSheet.Descendants(Spreadsheet + "t").Select(cell => cell.Value)
            .Should().Contain("=HYPERLINK(\"https://example.com\") & <Sách>")
            .And.Contain("0123456789012");
        itemSheet.Descendants(Spreadsheet + "f").Should().BeEmpty();
        itemSheet.Descendants(Spreadsheet + "v").Select(cell => cell.Value).Should().Contain("270000");
    }

    [Fact]
    public void Export_EmptyResult_HasHeadersAndZeroStatistics()
    {
        var bytes = OrderExcelExporter.Export([], new AdminOrdersQuery(), TestContext.Current.CancellationToken);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream);
        Read(archive, "xl/worksheets/sheet1.xml").Descendants(Spreadsheet + "v")
            .Select(cell => cell.Value).Should().Equal("0", "0", "0");
        Read(archive, "xl/worksheets/sheet2.xml").Descendants(Spreadsheet + "row").Should().ContainSingle();
        Read(archive, "xl/worksheets/sheet3.xml").Descendants(Spreadsheet + "row").Should().ContainSingle();
    }

    [Fact]
    public void Export_CancelledRequest_StopsGeneratingFile()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => OrderExcelExporter.Export([], new AdminOrdersQuery(), cancellation.Token);
        act.Should().Throw<OperationCanceledException>();
    }

    private static XDocument Read(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Missing {path}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static Order CreateOrder(string code, int quantity, bool delivered)
    {
        var item = new OrderItem(1, "SKU-01", "=HYPERLINK(\"https://example.com\") & <Sách>", null,
            "0123456789012", "paperback", 100_000m, quantity);
        item.ApplyDiscount(10_000m * quantity);
        var order = Order.Create(code, null, "Khách", "0901234567", null,
            new ShippingAddress("Hà Nội", "Phường", "Đường"), PaymentMethod.Cod, null, null, null, [item],
            new OrderCharges(item.DiscountAmount, 30_000m, 30_000m, 5_000m, []), DateTime.UtcNow,
            TimeSpan.FromMinutes(30));
        if (delivered)
        {
            foreach (var status in OrderStatusMachine.PathTo(order.Status, OrderStatus.Delivered) ?? [])
            {
                order.ChangeStatus(status, null, null, DateTime.UtcNow);
            }
        }

        return order;
    }
}

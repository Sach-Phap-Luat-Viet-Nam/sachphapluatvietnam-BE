using AwesomeAssertions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class OrderNotificationFormatterTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_NewCodOrder_ShowsSummaryWithoutSensitiveDetails()
    {
        var text = OrderNotificationFormatter.Format(
            OrderNotificationKind.NewOrder, OrderNotificationTests.CreateOrder(), "https://admin.example.com/");

        text.Should().Be(
            """
            🛒 ĐƠN MỚI #SLVN2610270001
            💵 480.000đ · COD · 3 cuốn
            ━━━━━━━━━━━━
            👤 Nguyễn V. An · 09xxxxx678
            📍 Phường Cầu Giấy, Hà Nội
            📚 Sách:
            • [SLVN-A-BM] Bộ luật Dân sự ×2
            • [SLVN-B-BM] Luật Đất đai ×1
            ━━━━━━━━━━━━
            ➡️ Gọi khách xác nhận đơn
            🔗 Xem đơn: https://admin.example.com/don-hang/SLVN2610270001
            """.ReplaceLineEndings("\n"));
        text.Should().NotContain("0912345678").And.NotContain("Xuân Thủy");
    }

    [Fact]
    public void Format_Paid_UsesPaidHeaderAndOmitsLinkWhenNoAdminUrl()
    {
        var order = OrderNotificationTests.CreateOrder(PaymentMethod.BankTransfer);
        order.ChangeStatus(OrderStatus.Paid, null, null, Now);

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.Paid, order, adminBaseUrl: null);

        text.Should().StartWith("💰 ĐÃ THANH TOÁN #SLVN2610270001\n💵 480.000đ · Chuyển khoản · 3 cuốn");
        text.Should().EndWith("➡️ Xác nhận và đóng gói đơn");
    }

    [Fact]
    public void Format_CancelledByCustomer_IncludesReasonAndStockNote()
    {
        var order = OrderNotificationTests.CreateOrder();
        order.ChangeStatus(OrderStatus.Cancelled, null, "Đặt nhầm địa chỉ", Now);

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.CancelledByCustomer, order, null);

        text.Should().StartWith("❌ KHÁCH HỦY ĐƠN #SLVN2610270001");
        text.Should().Contain("📝 Lý do: Đặt nhầm địa chỉ");
        text.Should().EndWith("ℹ️ Tồn kho đã tự hoàn lại, không cần xử lý thêm");
    }

    [Fact]
    public void Format_CancelledAfterBankTransferPaid_AsksForRefund()
    {
        var order = OrderNotificationTests.CreateOrder(PaymentMethod.BankTransfer);
        order.ChangeStatus(OrderStatus.Paid, null, null, Now);
        order.ChangeStatus(OrderStatus.Confirmed, 9, null, Now);
        order.ChangeStatus(OrderStatus.Cancelled, null, null, Now);

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.CancelledByCustomer, order, null);

        text.Should().EndWith("⚠️ Khách đã chuyển khoản: cần hoàn tiền");
        text.Should().NotContain("Lý do");
    }

    [Fact]
    public void Format_ManyTitles_ListsFirstFourAndCountsTheRest()
    {
        var items = Enumerable.Range(1, 7)
            .Select(i => new OrderItem(
                i, $"SKU-{i}", $"Sách số {i}", i == 1 ? "Tái bản 2026" : null, "9780306406157", "paperback",
                100_000m, 1))
            .ToList();
        var order = Order.Create(
            "SLVN2610270009", null, "Lê Bình", "0912345678", null,
            new ShippingAddress("Hà Nội", "Phường Cầu Giấy", "1 Xuân Thủy"), PaymentMethod.Cod, null, null, null,
            items, OrderCharges.Shipping(0m), Now, TimeSpan.FromMinutes(30));

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.NewOrder, order, null);

        text.Should().Contain("• [SKU-1] Sách số 1 (Tái bản 2026) ×1\n• [SKU-2] Sách số 2 ×1\n"
            + "• [SKU-3] Sách số 3 ×1\n• [SKU-4] Sách số 4 ×1\n• … và 3 sách khác");
        text.Should().NotContain("Sách số 5");
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "Nguyễn V. An")]
    [InlineData("Trần Thị Thu Hà", "Trần T. T. Hà")]
    [InlineData("  Lê   Bình ", "Lê Bình")]
    [InlineData("Minh", "Minh")]
    public void ShortenName_KeepsFamilyAndGivenName(string fullName, string expected)
    {
        OrderNotificationFormatter.ShortenName(fullName).Should().Be(expected);
    }

    [Theory]
    [InlineData("0912345678", "09xxxxx678")]
    [InlineData("02838123456", "02xxxxxx456")]
    [InlineData("12345", "xxxxx")]
    public void MaskPhone_KeepsPrefixAndLastThreeDigits(string phone, string expected)
    {
        OrderNotificationFormatter.MaskPhone(phone).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0đ")]
    [InlineData(30_000, "30.000đ")]
    [InlineData(1_250_000, "1.250.000đ")]
    public void FormatMoney_UsesVietnameseThousandsSeparator(decimal amount, string expected)
    {
        OrderNotificationFormatter.FormatMoney(amount).Should().Be(expected);
    }
}

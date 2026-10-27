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
            🛒 ĐƠN MỚI #SLVN2610270001 – COD
            Khách: Nguyễn V. An – 09xxxxx678
            3 cuốn – Tổng: 480.000đ
            Giao: Phường Cầu Giấy, Hà Nội
            👉 https://admin.example.com/don-hang/SLVN2610270001
            """.ReplaceLineEndings("\n"));
        text.Should().NotContain("0912345678").And.NotContain("Xuân Thủy");
    }

    [Fact]
    public void Format_Paid_UsesPaidHeaderAndOmitsLinkWhenNoAdminUrl()
    {
        var order = OrderNotificationTests.CreateOrder(PaymentMethod.BankTransfer);
        order.ChangeStatus(OrderStatus.Paid, null, null, Now);

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.Paid, order, adminBaseUrl: null);

        text.Should().StartWith("💰 ĐÃ THANH TOÁN #SLVN2610270001 – Chuyển khoản");
        text.Should().NotContain("👉");
    }

    [Fact]
    public void Format_CancelledByCustomer_IncludesReason()
    {
        var order = OrderNotificationTests.CreateOrder();
        order.ChangeStatus(OrderStatus.Cancelled, null, "Đặt nhầm địa chỉ", Now);

        var text = OrderNotificationFormatter.Format(OrderNotificationKind.CancelledByCustomer, order, null);

        text.Should().StartWith("❌ KHÁCH HỦY ĐƠN #SLVN2610270001 – COD");
        text.Should().EndWith("Lý do: Đặt nhầm địa chỉ");
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

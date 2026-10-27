using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class SepayPaymentServiceTests
{
    private const string ApiKey = "test-sepay-key";

    private static readonly DateTime Now = new(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Apikey test-sepay-key")]
    [InlineData("apikey test-sepay-key")]
    [InlineData("Apikey   test-sepay-key  ")]
    public void EnsureAuthorized_ValidKey_DoesNotThrow(string header)
    {
        var act = () => CreateService().EnsureAuthorized(header);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apikey wrong-key")]
    [InlineData("Apikey")]
    [InlineData("Bearer test-sepay-key")]
    [InlineData("test-sepay-key")]
    public void EnsureAuthorized_InvalidHeader_ThrowsUnauthorized(string? header)
    {
        var act = () => CreateService().EnsureAuthorized(header);

        var exception = act.Should().Throw<BusinessException>().Which;
        exception.Code.Should().Be(OrderErrors.SepayUnauthorizedCode);
        exception.StatusCode.Should().Be(401);
    }

    [Fact]
    public void EnsureAuthorized_ApiKeyNotConfigured_RejectsEvenEmptyKey()
    {
        var service = CreateService(new SepayOptions { ApiKey = string.Empty });

        var act = () => service.EnsureAuthorized("Apikey ");

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(OrderErrors.SepayUnauthorizedCode);
    }

    [Fact]
    public void BuildInstructions_PendingBankTransfer_ReturnsAccountAndQr()
    {
        var instructions = CreateService().BuildInstructions(CreateOrder(PaymentMethod.BankTransfer));

        instructions.Should().BeEquivalentTo(new PaymentInstructionsResponse(
            "MBBank",
            "0123456789",
            "CONG TY SACH LUAT VIET NAM",
            480_000m,
            "SLVN2610230001",
            "https://qr.sepay.vn/img?acc=0123456789&bank=MBBank&amount=480000&des=SLVN2610230001"));
    }

    [Fact]
    public void BuildInstructions_Cod_ReturnsNull()
    {
        CreateService().BuildInstructions(CreateOrder(PaymentMethod.Cod)).Should().BeNull();
    }

    [Fact]
    public void BuildInstructions_OrderNoLongerPending_ReturnsNull()
    {
        var order = CreateOrder(PaymentMethod.BankTransfer);
        order.ChangeStatus(OrderStatus.Paid, null, null, Now);

        CreateService().BuildInstructions(order).Should().BeNull();
    }

    [Fact]
    public void BuildInstructions_AccountNotConfigured_ReturnsNull()
    {
        var service = CreateService(new SepayOptions { ApiKey = ApiKey });

        service.BuildInstructions(CreateOrder(PaymentMethod.BankTransfer)).Should().BeNull();
    }

    [Theory]
    [InlineData("SLVN2610230001", "SLVN2610230001")]
    [InlineData("thanh toan don slvn2610230001 nguyen van a", "SLVN2610230001")]
    [InlineData("MBVCB.123.SLVN2610230001.CT tu NGUYEN VAN A", "SLVN2610230001")]
    [InlineData("SLVN26102300012 mo rong so thu tu", "SLVN26102300012")]
    [InlineData("SLVN12345 thieu so", null)]
    [InlineData("chuyen tien mua sach", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractOrderCode_FindsCodeInTransferContent(string? text, string? expected)
    {
        SepayPaymentService.ExtractOrderCode(text).Should().Be(expected);
    }

    private static SepayPaymentService CreateService(SepayOptions? options = null)
    {
        var dbOptions = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        var dbContext = new OrderingDbContext(dbOptions);
        var timeProvider = new FakeTimeProvider(Now);
        var statusService = new OrderStatusService(
            dbContext,
            Substitute.For<IInventoryService>(),
            Substitute.For<IPromotionService>(),
            new OrderNotificationOutbox(dbContext, Substitute.For<IStaffNotifier>(), timeProvider),
            timeProvider,
            NullLogger<OrderStatusService>.Instance);
        return new SepayPaymentService(
            dbContext,
            statusService,
            Options.Create(options ?? DefaultOptions),
            timeProvider,
            NullLogger<SepayPaymentService>.Instance);
    }

    private static SepayOptions DefaultOptions => new()
    {
        ApiKey = ApiKey,
        AccountNumber = "0123456789",
        BankName = "MBBank",
        AccountName = "CONG TY SACH LUAT VIET NAM",
    };

    private static Order CreateOrder(PaymentMethod paymentMethod)
    {
        var items = new[]
        {
            new OrderItem(1, "SLVN-A-BM", "Bộ luật Dân sự", null, "9780306406157", "paperback", 150_000m, 2),
            new OrderItem(2, "SLVN-B-BM", "Luật Đất đai", null, "9786041234567", "paperback", 150_000m, 1),
        };
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
            OrderCharges.Shipping(30_000m),
            Now,
            TimeSpan.FromMinutes(30));
    }
}

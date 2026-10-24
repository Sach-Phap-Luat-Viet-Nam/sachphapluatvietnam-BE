using AwesomeAssertions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class BuyNowOrderRequestValidatorTests
{
    [Fact]
    public void Valid_IsValid()
    {
        var result = new BuyNowOrderRequestValidator().Validate(Request());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveQuantity_IsInvalid(int quantity)
    {
        var result = new BuyNowOrderRequestValidator().Validate(Request(quantity: quantity));

        result.Errors.Should().Contain(error => error.PropertyName == "Quantity");
    }

    [Fact]
    public void QuantityOverPerSkuLimit_IsLeftToService()
    {
        // Trần 20 cuốn/SKU lấy từ cấu hình, service trả MAX_QTY_EXCEEDED (422) thay vì VALIDATION_ERROR.
        var result = new BuyNowOrderRequestValidator().Validate(Request(quantity: 21));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void MissingSku_IsInvalid()
    {
        var result = new BuyNowOrderRequestValidator().Validate(Request(skuId: 0));

        result.Errors.Should().Contain(error => error.PropertyName == "SkuId");
    }

    [Fact]
    public void CheckoutRules_AreSharedWithCartOrder()
    {
        var result = new BuyNowOrderRequestValidator().Validate(Request(phone: "12345") with
        {
            CustomerName = "",
            InvoiceInfo = new InvoiceInfoDto("Công ty A", "123", "Hà Nội", "ketoan@a.vn"),
        });

        result.Errors.Select(error => error.PropertyName).Should()
            .Contain(["CustomerName", "Phone", "InvoiceInfo.TaxCode"]);
    }

    private static BuyNowOrderRequest Request(long skuId = 7, int quantity = 1, string phone = "0901234567") => new(
        skuId,
        quantity,
        "Nguyễn Văn A",
        phone,
        Email: null,
        new ShippingAddressDto("Hà Nội", "Phường Cầu Giấy", "1 Xuân Thủy"),
        PaymentMethod.Cod,
        InvoiceInfo: null,
        Note: null);
}

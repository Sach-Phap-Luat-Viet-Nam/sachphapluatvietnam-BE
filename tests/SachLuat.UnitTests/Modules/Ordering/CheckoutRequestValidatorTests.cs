using AwesomeAssertions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class CheckoutRequestValidatorTests
{
    [Fact]
    public void Preview_Cart_IsValidWithoutSku()
    {
        var request = new CheckoutPreviewRequest(CheckoutSource.Cart, null, null, "Hà Nội", "MUATHI2026", null);

        new CheckoutPreviewRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, 1)]
    [InlineData(5L, 0)]
    [InlineData(5L, null)]
    public void Preview_BuyNow_NeedsSkuAndPositiveQuantity(long? skuId, int? quantity)
    {
        var request = new CheckoutPreviewRequest(CheckoutSource.BuyNow, skuId, quantity, null, null, null);

        new CheckoutPreviewRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Preview_BuyNow_Valid()
    {
        var request = new CheckoutPreviewRequest(CheckoutSource.BuyNow, 5, 2, "Hà Nội", null, "0901234567");

        new CheckoutPreviewRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Preview_InvalidPhoneOrLongCoupon_IsInvalid()
    {
        var request = new CheckoutPreviewRequest(CheckoutSource.Cart, null, null, null, new string('A', 41), "123");

        var result = new CheckoutPreviewRequestValidator().Validate(request);

        result.Errors.Select(error => error.PropertyName).Should().Contain(["CouponCode", "Phone"]);
    }

    [Fact]
    public void Order_CouponAndExpectedTotal_AreValidated()
    {
        var validator = new CreateOrderRequestValidator();

        validator.Validate(Order() with { CouponCode = "MUATHI2026", ExpectedTotal = 360_000m }).IsValid
            .Should().BeTrue();
        validator.Validate(Order() with { ExpectedTotal = -1m }).Errors
            .Should().Contain(error => error.PropertyName == "ExpectedTotal");
        validator.Validate(Order() with { CouponCode = new string('A', 41) }).Errors
            .Should().Contain(error => error.PropertyName == "CouponCode");
    }

    [Fact]
    public void ShippingQuote_LegacySubtotalOnly_StillValid()
    {
        var validator = new ShippingQuoteRequestValidator();

        validator.Validate(new ShippingQuoteRequest(250_000m)).IsValid.Should().BeTrue();
        validator.Validate(new ShippingQuoteRequest(-1m)).IsValid.Should().BeFalse();
        validator.Validate(new ShippingQuoteRequest(0m, "Hà Nội", [new QuoteItem(1, 2)])).IsValid.Should().BeTrue();
        validator.Validate(new ShippingQuoteRequest(0m, null, [new QuoteItem(0, 2)])).IsValid.Should().BeFalse();
        validator.Validate(new ShippingQuoteRequest(0m, null, [new QuoteItem(1, 0)])).IsValid.Should().BeFalse();
    }

    private static CreateOrderRequest Order() => new(
        "Nguyễn Văn A",
        "0901234567",
        Email: null,
        new ShippingAddressDto("Hà Nội", "Phường Cầu Giấy", "1 Xuân Thủy"),
        PaymentMethod.Cod,
        InvoiceInfo: null,
        Note: null);
}

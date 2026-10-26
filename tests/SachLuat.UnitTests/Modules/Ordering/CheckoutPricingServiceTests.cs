using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Ordering;

public sealed class CheckoutPricingServiceTests
{
    private static readonly PromotionCustomer Customer = new(UserId: null, Phone: "0901234567");

    private readonly ICatalogSkuReader _skuReader = Substitute.For<ICatalogSkuReader>();
    private readonly IShippingQuoteService _shipping = Substitute.For<IShippingQuoteService>();
    private readonly IPromotionService _promotions = Substitute.For<IPromotionService>();
    private readonly CheckoutPricingService _service;

    public CheckoutPricingServiceTests()
    {
        _service = new CheckoutPricingService(
            _skuReader, _shipping, _promotions, Options.Create(new OrderingOptions { MaxQuantityPerSku = 20 }));
        _skuReader.GetSkusAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>()).Returns(
            new Dictionary<long, SkuInfo>
            {
                [1] = Sku(1, price: 150_000m, regular: 150_000m, weight: 500, bookId: 11, categoryIds: [3, 1]),
                [2] = Sku(2, price: 120_000m, regular: 150_000m, weight: null, bookId: 12),
                [3] = Sku(3, price: 100_000m, regular: 100_000m, weight: 300, sellable: false),
            });
        _promotions.EvaluateAsync(Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>())
            .Returns(PromotionEvaluation.None);
        SetShipping(fee: 30_000m);
    }

    [Fact]
    public async Task Price_WithoutPromotions_TotalsSubtotalPlusShipping()
    {
        var pricing = await PriceAsync([new StockLine(1, 2), new StockLine(2, 1)], couponCode: null);

        pricing.Subtotal.Should().Be(420_000m);
        pricing.Discount.Should().Be(0m);
        pricing.Total.Should().Be(450_000m);
        pricing.CouponApplied.Should().BeNull();
        pricing.Lines.Select(line => line.LineTotal).Should().Equal(300_000m, 120_000m);
    }

    [Fact]
    public async Task Price_AsksShippingForProvinceWeightsAndSubtotalBeforeDiscount()
    {
        await PriceAsync([new StockLine(1, 2), new StockLine(2, 1)], couponCode: null, province: "Hà Nội");

        await _shipping.Received(1).QuoteAsync(
            "Hà Nội",
            Arg.Is<IReadOnlyCollection<ShippingItem>>(items => HasWeights(items, new int?[] { 500, null })),
            420_000m,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_SendsPromotionTheShippingFeeLinesAndOnSaleFlags()
    {
        await PriceAsync([new StockLine(1, 2), new StockLine(2, 1)], couponCode: "MUATHI2026");

        await _promotions.Received(1).EvaluateAsync(
            Arg.Is<PromotionRequest>(request =>
                request.ShippingFee == 30_000m
                && request.CouponCode == "MUATHI2026"
                && request.Customer == Customer
                && request.Lines.Count == 2
                && request.Lines[0].BookId == 11 && request.Lines[0].CategoryIds.SequenceEqual(new long[] { 3, 1 })
                && !request.Lines[0].IsOnSale
                && request.Lines[1].IsOnSale), // giá hiện hành 120.000 < giá thường 150.000 (đang có lịch giá)
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_AppliesPromotionDiscountsToLinesAndTotal()
    {
        SetShipping(fee: 30_000m);
        _promotions.EvaluateAsync(Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>()).Returns(
            new PromotionEvaluation(
                OrderDiscount: 84_000m,
                ShippingDiscount: 30_000m,
                new Dictionary<long, decimal> { [1] = 60_000m, [2] = 24_000m },
                [
                    new AppliedPromotion(5, "Giảm 20%", PromotionKind.OrderDiscount, null, null, 84_000m),
                    new AppliedPromotion(6, "Freeship", PromotionKind.FreeShipping, 9, "FREESHIP", 30_000m),
                ],
                CouponApplied: true));

        var pricing = await PriceAsync([new StockLine(1, 2), new StockLine(2, 1)], couponCode: "FREESHIP");

        pricing.Total.Should().Be(420_000m - 84_000m + 30_000m - 30_000m);
        pricing.Lines.Select(line => line.Discount).Should().Equal(60_000m, 24_000m);
        pricing.CouponApplied.Should().BeTrue();

        var charges = pricing.ToCharges();
        charges.Discount.Should().Be(84_000m);
        charges.ShippingDiscount.Should().Be(30_000m);
        charges.AppliedPromotions.Select(promotion => (promotion.Kind, promotion.CouponCode, promotion.Amount))
            .Should().Equal(("order_discount", null, 84_000m), ("free_shipping", "FREESHIP", 30_000m));
    }

    [Fact]
    public async Task Price_ComboOnListPrice_RaisesSubtotalAndUnitPrices_ButNotWhatTheCustomerPays()
    {
        // Hai sách giá niêm yết 200.000đ, hiện hành 150.000đ và 120.000đ (tổng 270.000đ); combo giảm thật 20.000đ
        // (khách trả 250.000đ) và chênh lệch niêm yết 50.000đ + 80.000đ: tiền giảm combo hiển thị 150.000đ.
        _promotions.EvaluateAsync(Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>()).Returns(
            new PromotionEvaluation(
                OrderDiscount: 150_000m,
                ShippingDiscount: 0m,
                new Dictionary<long, decimal> { [1] = 60_000m, [2] = 90_000m },
                [new AppliedPromotion(5, "Combo", PromotionKind.Combo, null, null, 150_000m)],
                CouponApplied: false)
            {
                ListMarkups = new Dictionary<long, decimal> { [1] = 50_000m, [2] = 80_000m },
            });

        var pricing = await PriceAsync([new StockLine(1, 1), new StockLine(2, 1)], couponCode: null);

        pricing.Lines.Select(line => line.UnitPrice).Should().Equal(200_000m, 200_000m);
        pricing.Lines.Select(line => line.LineTotal).Should().Equal(200_000m, 200_000m);
        pricing.Subtotal.Should().Be(400_000m);
        pricing.Discount.Should().Be(150_000m);
        pricing.Total.Should().Be(280_000m, "270.000đ − 20.000đ giảm thật + 30.000đ ship, như khi chưa tính theo giá niêm yết");
        // Phí ship vẫn tính trên tạm tính theo giá hiện hành.
        await _shipping.Received(1).QuoteAsync(
            Arg.Any<string?>(), Arg.Any<IReadOnlyCollection<ShippingItem>>(), 270_000m, Arg.Any<CancellationToken>());
        await _promotions.Received(1).EvaluateAsync(
            Arg.Is<PromotionRequest>(request => request.Lines.All(line => line.ListPrice == 200_000m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_WithoutComboMarkup_KeepsTheCurrentPricesAsUnitPrices()
    {
        var pricing = await PriceAsync([new StockLine(1, 2), new StockLine(2, 1)], couponCode: null);

        pricing.Lines.Select(line => line.UnitPrice).Should().Equal(150_000m, 120_000m);
    }

    [Fact]
    public async Task Price_FreeShippingByThreshold_ChargesNothingButKeepsBaseFee()
    {
        SetShipping(fee: 0m, baseFee: 30_000m);

        var pricing = await PriceAsync([new StockLine(1, 3)], couponCode: null);

        pricing.Total.Should().Be(450_000m);
        pricing.ToCharges().Should().Match<OrderCharges>(charges =>
            charges.ShippingFee == 0m && charges.ShippingBaseFee == 30_000m);
    }

    [Fact]
    public async Task Price_CouponTypedButNotChosen_ReportsNotApplied()
    {
        var pricing = await PriceAsync([new StockLine(1, 1)], couponCode: "MUATHI2026");

        pricing.CouponApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Price_UnsellableSku_ThrowsTheCallersError()
    {
        var act = () => PriceAsync([new StockLine(3, 1)], couponCode: null);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(OrderErrors.SkuNotAvailableCode);
        await _promotions.DidNotReceive().EvaluateAsync(Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_UnknownSku_Throws()
    {
        var act = () => PriceAsync([new StockLine(99, 1)], couponCode: null);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(OrderErrors.SkuNotAvailableCode);
    }

    [Fact]
    public async Task Price_QuantityOverLimit_ThrowsBeforeReadingCatalog()
    {
        var act = () => PriceAsync([new StockLine(1, 21)], couponCode: null);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(OrderErrors.MaxQuantityExceededCode);
        await _skuReader.DidNotReceive()
            .GetSkusAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Price_PropagatesCouponErrorsFromPromotion()
    {
        _promotions.EvaluateAsync(Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>())
            .Returns<PromotionEvaluation>(_ => throw new BusinessException("COUPON_EXPIRED", "Mã hết hạn"));

        var act = () => PriceAsync([new StockLine(1, 1)], couponCode: "OLD");

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("COUPON_EXPIRED");
    }

    // Hàm riêng vì NSubstitute nhận biểu thức cây (expression tree), không dùng được tuple literal trong đó.
    private static bool HasWeights(IReadOnlyCollection<ShippingItem> items, int?[] weights) =>
        items.Select(item => item.WeightGram).SequenceEqual(weights);

    private Task<CheckoutPricing> PriceAsync(
        IReadOnlyList<StockLine> lines,
        string? couponCode,
        string? province = "Hà Nội") => _service.PriceAsync(
            lines, province, couponCode, Customer, OrderErrors.SkuNotAvailable, CancellationToken.None);

    private void SetShipping(decimal fee, decimal? baseFee = null) =>
        _shipping.QuoteAsync(
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyCollection<ShippingItem>>(),
                Arg.Any<decimal>(),
                Arg.Any<CancellationToken>())
            .Returns(new ShippingQuote(
                fee, baseFee ?? fee, IsFreeByThreshold: fee == 0m, 300_000m, 0m, 1100, "Toàn quốc", 2, 5));

    private static SkuInfo Sku(
        long id,
        decimal price,
        decimal regular,
        int? weight,
        long bookId = 1,
        long[]? categoryIds = null,
        bool sellable = true) => new(
            id, $"SLVN-{id}-BM", bookId, $"Sách {id}", $"sach-{id}", id, null, "9780306406157", "paperback",
            ListPrice: 200_000m, price, regular, weight, null, IsLatestEdition: true, categoryIds ?? [], sellable);
}

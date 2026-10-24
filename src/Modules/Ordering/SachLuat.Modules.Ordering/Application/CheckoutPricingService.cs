using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Application;

internal sealed record PricedLine(SkuInfo Sku, int Quantity, decimal Discount)
{
    public decimal LineTotal => Sku.SalePrice * Quantity;
}

/// <param name="CouponApplied">null khi khách không nhập mã.</param>
internal sealed record CheckoutPricing(
    IReadOnlyList<PricedLine> Lines,
    decimal Subtotal,
    PromotionEvaluation Promotions,
    ShippingQuote Shipping,
    bool? CouponApplied)
{
    public decimal Discount => Promotions.OrderDiscount;

    public decimal ShippingDiscount => Promotions.ShippingDiscount;

    public decimal Total => Subtotal - Discount + Shipping.Fee - ShippingDiscount;

    public OrderCharges ToCharges() => new(
        Discount,
        Shipping.Fee,
        Shipping.BaseFee,
        ShippingDiscount,
        [.. Promotions.Applied.Select(applied => new AppliedPromotionSnapshot(
            applied.PromotionId,
            applied.Name,
            SnakeCaseEnumConverter<PromotionKind>.ToProvider(applied.Kind),
            applied.CouponCode,
            applied.Amount))]);
}

/// <summary>
/// Cách tính tiền duy nhất của thanh toán: dùng chung cho xem trước và tạo đơn nên hai nơi không thể lệch nhau.
/// Thứ tự: tạm tính → phí ship (theo tỉnh, trọng lượng, ngưỡng miễn phí trên tạm tính) → khuyến mãi.
/// </summary>
internal sealed class CheckoutPricingService(
    ICatalogSkuReader skuReader,
    IShippingQuoteService shippingQuoteService,
    IPromotionService promotionService,
    IOptions<OrderingOptions> options)
{
    private readonly OrderingOptions _options = options.Value;

    public async Task<CheckoutPricing> PriceAsync(
        IReadOnlyList<StockLine> lines,
        string? province,
        string? couponCode,
        PromotionCustomer customer,
        Func<BusinessException> skuNotAvailable,
        CancellationToken cancellationToken)
    {
        if (lines.Any(line => line.Quantity > _options.MaxQuantityPerSku))
        {
            throw OrderErrors.MaxQuantityExceeded(_options.MaxQuantityPerSku);
        }

        var skus = await skuReader.GetSkusAsync(lines.Select(line => line.SkuId).ToList(), cancellationToken);
        if (lines.Any(line => !skus.TryGetValue(line.SkuId, out var sku) || !sku.IsSellable))
        {
            throw skuNotAvailable();
        }

        var subtotal = lines.Sum(line => skus[line.SkuId].SalePrice * line.Quantity);
        var quote = await shippingQuoteService.QuoteAsync(
            province,
            [.. lines.Select(line => new ShippingItem(skus[line.SkuId].WeightGram, line.Quantity))],
            subtotal,
            cancellationToken);

        var evaluation = await promotionService.EvaluateAsync(
            new PromotionRequest(
                [.. lines.Select(line =>
                {
                    var sku = skus[line.SkuId];
                    return new PromotionLine(
                        sku.SkuId,
                        sku.BookId,
                        sku.CategoryIds,
                        sku.SalePrice,
                        line.Quantity,
                        IsOnSale: sku.SalePrice < sku.RegularSalePrice);
                })],
                quote.Fee,
                customer,
                couponCode),
            cancellationToken);

        var pricedLines = lines
            .Select(line => new PricedLine(
                skus[line.SkuId], line.Quantity, evaluation.LineDiscounts.GetValueOrDefault(line.SkuId)))
            .ToList();
        return new CheckoutPricing(
            pricedLines,
            subtotal,
            evaluation,
            quote,
            string.IsNullOrWhiteSpace(couponCode) ? null : evaluation.CouponApplied);
    }
}

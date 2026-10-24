using SachLuat.Modules.Shipping.Contracts;

namespace SachLuat.Modules.Shipping.Domain;

/// <summary>Phần thuần của báo giá (không truy cập CSDL) để kiểm thử riêng.</summary>
internal static class ShippingQuoteCalculator
{
    /// <summary>Tổng trọng lượng đơn = Σ trọng lượng × số lượng + bao bì (mục 12.2).</summary>
    public static int TotalWeightGram(IEnumerable<ShippingItem> items, ShippingSettingsValues settings) =>
        items.Sum(item => (item.WeightGram ?? settings.DefaultBookWeightGram) * item.Quantity)
        + settings.PackagingWeightGram;

    /// <param name="zone">null: không có vùng, dùng phí cố định (chưa chọn tỉnh hoặc chưa cấu hình vùng nào).</param>
    public static ShippingQuote Calculate(
        ShippingSettingsValues settings,
        ShippingZone? zone,
        IReadOnlyCollection<ShippingItem> items,
        decimal subtotal)
    {
        var weight = TotalWeightGram(items, settings);
        var baseFee = zone?.CalculateFee(weight) ?? settings.FlatFee;

        // Tính trên tạm tính trước giảm giá: chương trình giảm không làm khách mất freeship.
        var isFree = settings.FreeShippingThreshold is { } threshold && subtotal >= threshold;
        var remaining = settings.FreeShippingThreshold is { } target && !isFree ? target - subtotal : 0m;

        return new ShippingQuote(
            isFree ? 0m : baseFee,
            baseFee,
            isFree,
            settings.FreeShippingThreshold,
            remaining,
            weight,
            zone?.Name,
            zone?.EtaMinDays,
            zone?.EtaMaxDays);
    }
}

using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Ordering.Application;

internal sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    /// <summary>Phí ship cố định khi chưa tích hợp API đơn vị vận chuyển (FR-34).</summary>
    [Range(0, 10_000_000)]
    public decimal ShippingFlatFee { get; set; } = 30_000m;

    /// <summary>BR-14: miễn phí vận chuyển cho đơn từ ngưỡng này (giả định 300.000đ).</summary>
    [Range(0, 1_000_000_000)]
    public decimal FreeShippingThreshold { get; set; } = 300_000m;

    /// <summary>BR-10 bước 4: quá hạn chưa thanh toán online thì tự hủy, hoàn tồn tạm giữ.</summary>
    [Range(1, 1440)]
    public int PaymentTimeoutMinutes { get; set; } = 30;

    /// <summary>FR-31: tối đa số cuốn mỗi SKU trong một đơn lẻ.</summary>
    [Range(1, 1000)]
    public int MaxQuantityPerSku { get; set; } = 20;

    /// <summary>Bật job tự hủy đơn quá hạn thanh toán (tắt trong test không có CSDL).</summary>
    public bool EnableExpiredPaymentSweep { get; set; } = true;

    [Range(5, 3600)]
    public int ExpiredPaymentSweepSeconds { get; set; } = 60;
}

internal sealed class ShippingFeeCalculator(Microsoft.Extensions.Options.IOptions<OrderingOptions> options)
{
    private readonly OrderingOptions _options = options.Value;

    public decimal FreeShippingThreshold => _options.FreeShippingThreshold;

    public decimal Calculate(decimal subtotal) =>
        subtotal >= _options.FreeShippingThreshold ? 0 : _options.ShippingFlatFee;
}

using System.ComponentModel.DataAnnotations;
using SachLuat.Modules.Shipping.Domain;

namespace SachLuat.Modules.Shipping.Application;

/// <summary>
/// Giá trị mặc định khi Admin chưa lưu cấu hình vận chuyển (bảng <c>shipping_settings</c> chưa có dòng nào).
/// Sau khi Admin lưu lần đầu, CSDL là nguồn duy nhất.
/// </summary>
internal sealed class ShippingOptions
{
    public const string SectionName = "Shipping";

    /// <summary>Phí cố định mặc định (trước đây là <c>Ordering:ShippingFlatFee</c>).</summary>
    [Range(0, 10_000_000)]
    public decimal DefaultFlatFee { get; set; } = 30_000m;

    /// <summary>BR-14: ngưỡng miễn phí mặc định (giả định 300.000đ); trước đây là <c>Ordering:FreeShippingThreshold</c>.</summary>
    [Range(0, 1_000_000_000)]
    public decimal DefaultFreeShippingThreshold { get; set; } = 300_000m;

    /// <summary>Mục 12.2: bao bì 100 g.</summary>
    [Range(0, ShippingSettings.MaxWeightGram)]
    public int DefaultPackagingWeightGram { get; set; } = 100;

    /// <summary>Sách luật bìa mềm trung bình; dùng khi ấn bản chưa nhập trọng lượng.</summary>
    [Range(1, ShippingSettings.MaxWeightGram)]
    public int DefaultBookWeightGram { get; set; } = 400;

    public ShippingSettingsValues ToValues() => new(
        DefaultFreeShippingThreshold, DefaultPackagingWeightGram, DefaultBookWeightGram, DefaultFlatFee);
}

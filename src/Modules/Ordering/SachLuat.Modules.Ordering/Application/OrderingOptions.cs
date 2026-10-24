using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Ordering.Application;

internal sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

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

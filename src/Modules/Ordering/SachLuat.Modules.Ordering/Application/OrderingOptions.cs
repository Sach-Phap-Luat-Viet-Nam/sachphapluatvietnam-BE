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

    /// <summary>Tối đa số đơn COD chưa giao xong của một số điện thoại (chống đặt trùng); 0 = không giới hạn.</summary>
    [Range(0, 100)]
    public int MaxOpenCodOrdersPerPhone { get; set; } = 3;

    /// <summary>Số đơn COD giao thất bại ("bom hàng") để số điện thoại chỉ được chuyển khoản; 0 = không khóa.</summary>
    [Range(0, 100)]
    public int CodFailedDeliveryLimit { get; set; } = 2;

    /// <summary>Bật job gửi thông báo đơn cho nhân viên từ outbox (tắt trong test để gọi tay).</summary>
    public bool EnableStaffNotificationDispatch { get; set; } = true;

    [Range(1, 3600)]
    public int StaffNotificationPollSeconds { get; set; } = 10;

    /// <summary>Gửi lỗi quá số lần này thì bỏ cuộc (lùi dần 30 giây → tối đa 1 giờ giữa các lần).</summary>
    [Range(1, 20)]
    public int StaffNotificationMaxAttempts { get; set; } = 10;

    /// <summary>
    /// Địa chỉ trang Admin (ví dụ <c>https://admin.example.com</c>) để gắn link đơn vào tin nhắn; trống thì bỏ link.
    /// </summary>
    [Url]
    public string? AdminBaseUrl { get; set; }
}

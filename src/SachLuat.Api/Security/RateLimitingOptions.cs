namespace SachLuat.Api.Security;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Số request tối đa mỗi phút mỗi IP cho nhóm API đăng nhập/đăng ký.</summary>
    public int AuthPermitPerMinute { get; set; } = 10;

    /// <summary>Số request tối đa mỗi phút mỗi IP cho xem trước thanh toán (kèm thử mã giảm giá).</summary>
    public int CheckoutPermitPerMinute { get; set; } = 60;
}

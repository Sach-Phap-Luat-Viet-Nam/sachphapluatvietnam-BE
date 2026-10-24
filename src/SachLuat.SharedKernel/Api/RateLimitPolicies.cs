namespace SachLuat.SharedKernel.Api;

public static class RateLimitPolicies
{
    /// <summary>Đăng nhập, đăng ký, OTP (NFR-13): giới hạn theo IP.</summary>
    public const string Auth = "auth";

    /// <summary>Xem trước thanh toán và thử mã giảm giá: giới hạn theo IP để không dò mã.</summary>
    public const string Checkout = "checkout";
}

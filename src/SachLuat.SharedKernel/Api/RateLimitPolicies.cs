namespace SachLuat.SharedKernel.Api;

public static class RateLimitPolicies
{
    /// <summary>Đăng nhập, đăng ký, OTP (NFR-13): giới hạn theo IP.</summary>
    public const string Auth = "auth";
}

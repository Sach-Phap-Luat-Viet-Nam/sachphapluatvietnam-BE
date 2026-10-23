namespace SachLuat.SharedKernel.Security;

public static class AuthClaimTypes
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";

    /// <summary>Phương thức xác thực (RFC 8176): <c>pwd</c> hoặc <c>mfa</c>.</summary>
    public const string AuthenticationMethod = "amr";

    public const string Password = "pwd";
    public const string MultiFactor = "mfa";
}

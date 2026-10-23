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

    /// <summary>Đường đăng nhập: <c>portal</c> (tự đăng nhập) hoặc <c>admin</c> (Super Admin fast login).</summary>
    public const string LoginVia = "login_via";

    public const string LoginViaPortal = "portal";
    public const string LoginViaAdmin = "admin";

    /// <summary>Id Super Admin đã fast login vào tài khoản, chỉ có khi <c>login_via=admin</c>.</summary>
    public const string ImpersonatorId = "impersonator_id";
}

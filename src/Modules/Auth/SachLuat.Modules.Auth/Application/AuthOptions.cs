using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Auth.Application;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Khóa ký HMAC-SHA256, tối thiểu 32 ký tự. Lưu trong user-secrets / secret manager.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;
}

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>Thời hạn phiên khi không chọn "Ghi nhớ đăng nhập".</summary>
    [Range(1, 72)]
    public int SessionRefreshTokenHours { get; set; } = 12;

    /// <summary>Tên hiển thị trong ứng dụng authenticator (Google Authenticator, ...).</summary>
    public string TotpIssuer { get; set; } = "Sach Luat Viet Nam";

    /// <summary>Tạo Super Admin đầu tiên khi khởi động nếu chưa có (để trống thì bỏ qua).</summary>
    public BootstrapAdminOptions BootstrapAdmin { get; set; } = new();
}

internal sealed class BootstrapAdminOptions
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string FullName { get; set; } = "Super Admin";
}

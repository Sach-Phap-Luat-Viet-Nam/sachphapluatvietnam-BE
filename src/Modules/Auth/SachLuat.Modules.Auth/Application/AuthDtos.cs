using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Auth.Application;

internal sealed record RegisterRequest(string Email, string Password, string FullName, string? PhoneNumber);

/// <param name="Portal">App gọi đăng nhập; Super Admin chỉ đăng nhập được ở <see cref="LoginPortal.Admin"/>.</param>
internal sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false,
    LoginPortal Portal = LoginPortal.Storefront);

/// <summary>App phía FE gửi request đăng nhập (JSON: <c>storefront</c>, <c>admin</c>).</summary>
internal enum LoginPortal
{
    Storefront,
    Admin,
}

/// <param name="TrustDevice">
/// true: nhớ thiết bị này (cookie <c>sl_2fa_trust</c>) để các lần đăng nhập sau không phải nhập mã trong
/// <c>Auth:TrustedDeviceDays</c> ngày. Bỏ qua với Super Admin.
/// </param>
internal sealed record TwoFactorLoginRequest(string TwoFactorToken, string Code, bool TrustDevice = false);

internal sealed record TwoFactorCodeRequest(string Code);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record UpdateProfileRequest(
    string FullName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    Occupation? Occupation);

/// <param name="IsDefault">true: đặt làm địa chỉ mặc định (địa chỉ đầu tiên luôn là mặc định).</param>
internal sealed record UpsertAddressRequest(
    string RecipientName,
    string Phone,
    string Province,
    string Ward,
    string StreetAddress,
    bool IsDefault);

/// <param name="RecipientName">Điền vào <c>customerName</c> khi đặt hàng.</param>
/// <param name="Phone">Điền vào <c>phone</c> khi đặt hàng.</param>
internal sealed record AddressResponse(
    long Id,
    string RecipientName,
    string Phone,
    string Province,
    string Ward,
    string StreetAddress,
    bool IsDefault,
    DateTime UpdatedAt);

internal sealed record UpsertInvoiceInfoRequest(string CompanyName, string TaxCode, string Address, string Email);

internal sealed record InvoiceInfoResponse(
    string CompanyName,
    string TaxCode,
    string Address,
    string Email,
    DateTime UpdatedAt);

/// <summary>
/// Hồ sơ cho <c>GET/PATCH /me</c>: các field của <see cref="UserResponse"/> kèm dữ liệu để điền sẵn form đặt hàng.
/// </summary>
/// <param name="DefaultAddress">null khi sổ địa chỉ trống.</param>
/// <param name="InvoiceInfo">null khi chưa lưu thông tin hóa đơn.</param>
internal sealed record MeResponse(
    long Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    Occupation? Occupation,
    IReadOnlyList<string> Roles,
    bool TwoFactorEnabled,
    bool RequiresTwoFactorSetup,
    bool IsActive,
    ColorTheme? Theme,
    string? AvatarUrl,
    AddressResponse? DefaultAddress,
    InvoiceInfoResponse? InvoiceInfo);

internal sealed record CreateStaffRequest(string Email, string FullName, string Password, IReadOnlyList<string> Roles);

internal sealed record SetRolesRequest(IReadOnlyList<string> Roles);

internal sealed record ResetPasswordRequest(string NewPassword);

/// <summary>Lọc danh sách nhân viên/khách hàng: <see cref="Q"/> tìm trong email, họ tên, số điện thoại.</summary>
internal sealed class AdminUsersQuery : PagedQuery
{
    public string? Q { get; set; }
}

/// <summary>Chi tiết khách hàng cho Super Admin: tài khoản, sổ địa chỉ, thông tin hóa đơn (null khi chưa lưu).</summary>
internal sealed record AdminCustomerResponse(
    UserResponse Account,
    IReadOnlyList<AddressResponse> Addresses,
    InvoiceInfoResponse? InvoiceInfo);

/// <param name="RevokedSessions">Số phiên (refresh token còn hiệu lực) vừa bị thu hồi; 0 khi user không đăng nhập.</param>
internal sealed record LogoutUserResponse(int RevokedSessions);

internal sealed record UserResponse(
    long Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    Occupation? Occupation,
    IReadOnlyList<string> Roles,
    bool TwoFactorEnabled,
    bool RequiresTwoFactorSetup,
    bool IsActive,
    ColorTheme? Theme,
    string? AvatarUrl);

/// <param name="AvatarUrl"><c>secure_url</c> Cloudinary trả về sau khi upload bằng chữ ký của <c>/me/avatar</c>.</param>
internal sealed record SetAvatarRequest(string AvatarUrl);

internal sealed record AvatarResponse(string? AvatarUrl);

/// <param name="Theme">Giao diện storefront khách chọn (<c>dark</c>/<c>light</c>), ghi nhớ theo tài khoản.</param>
internal sealed record UpdatePreferencesRequest(ColorTheme? Theme);

internal sealed record PreferencesResponse(ColorTheme? Theme);

/// <param name="LoginVia"><c>portal</c> (tự đăng nhập) hoặc <c>admin</c> (Super Admin fast login).</param>
/// <param name="ImpersonatorId">Id Super Admin đang fast login, null khi <paramref name="LoginVia"/> = <c>portal</c>.</param>
internal sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserResponse User,
    string LoginVia,
    long? ImpersonatorId,
    int SessionVersion);

/// <summary>
/// <see cref="RequiresTwoFactor"/> = true: gọi tiếp <c>/auth/login/2fa</c> với <see cref="TwoFactorToken"/>;
/// ngược lại <see cref="Session"/> có access token.
/// </summary>
internal sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorToken, AuthResponse? Session);

internal sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri);

/// <summary>Refresh token cấp mới để controller ghi vào cookie HttpOnly.</summary>
internal sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt, bool IsPersistent);

internal sealed record AuthResult(AuthResponse Response, IssuedRefreshToken RefreshToken);

/// <summary>Token thiết bị tin cậy cấp mới để controller ghi vào cookie HttpOnly.</summary>
internal sealed record IssuedTrustedDevice(string Token, DateTime ExpiresAt);

/// <param name="TrustedDevice">null khi người dùng không chọn nhớ thiết bị hoặc tài khoản không được nhớ.</param>
internal sealed record TwoFactorLoginResult(AuthResult Session, IssuedTrustedDevice? TrustedDevice);

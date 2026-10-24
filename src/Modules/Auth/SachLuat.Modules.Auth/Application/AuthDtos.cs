using SachLuat.Modules.Auth.Domain;

namespace SachLuat.Modules.Auth.Application;

internal sealed record RegisterRequest(string Email, string Password, string FullName, string? PhoneNumber);

internal sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

internal sealed record TwoFactorLoginRequest(string TwoFactorToken, string Code);

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
    AddressResponse? DefaultAddress,
    InvoiceInfoResponse? InvoiceInfo);

internal sealed record CreateStaffRequest(string Email, string FullName, string Password, IReadOnlyList<string> Roles);

internal sealed record SetRolesRequest(IReadOnlyList<string> Roles);

internal sealed record UserResponse(
    long Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    DateOnly? BirthDate,
    Occupation? Occupation,
    IReadOnlyList<string> Roles,
    bool TwoFactorEnabled,
    bool RequiresTwoFactorSetup);

/// <param name="LoginVia"><c>portal</c> (tự đăng nhập) hoặc <c>admin</c> (Super Admin fast login).</param>
/// <param name="ImpersonatorId">Id Super Admin đang fast login, null khi <paramref name="LoginVia"/> = <c>portal</c>.</param>
internal sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserResponse User,
    string LoginVia,
    long? ImpersonatorId);

/// <summary>
/// <see cref="RequiresTwoFactor"/> = true: gọi tiếp <c>/auth/login/2fa</c> với <see cref="TwoFactorToken"/>;
/// ngược lại <see cref="Session"/> có access token.
/// </summary>
internal sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorToken, AuthResponse? Session);

internal sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri);

/// <summary>Refresh token cấp mới để controller ghi vào cookie HttpOnly.</summary>
internal sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt, bool IsPersistent);

internal sealed record AuthResult(AuthResponse Response, IssuedRefreshToken RefreshToken);

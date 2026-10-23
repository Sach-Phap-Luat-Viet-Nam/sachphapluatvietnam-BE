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

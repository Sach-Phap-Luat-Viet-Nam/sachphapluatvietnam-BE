using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Auth.Domain;

internal static class AuthErrors
{
    public const string InvalidCredentialsCode = "INVALID_CREDENTIALS";
    public const string AccountLockedCode = "ACCOUNT_LOCKED";
    public const string EmailTakenCode = "EMAIL_TAKEN";
    public const string InvalidRefreshTokenCode = "INVALID_REFRESH_TOKEN";
    public const string TwoFactorTokenInvalidCode = "TWO_FACTOR_TOKEN_INVALID";
    public const string OtpInvalidCode = "OTP_INVALID";
    public const string TwoFactorAlreadyEnabledCode = "TWO_FACTOR_ALREADY_ENABLED";
    public const string UserNotFoundCode = "USER_NOT_FOUND";
    public const string RoleInvalidCode = "ROLE_INVALID";
    public const string CannotRemoveOwnAdminCode = "CANNOT_REMOVE_OWN_ADMIN";
    public const string CannotImpersonateCode = "CANNOT_IMPERSONATE";
    public const string IdentityErrorCode = "IDENTITY_ERROR";
    public const string AddressNotFoundCode = "ADDRESS_NOT_FOUND";
    public const string AddressLimitReachedCode = "ADDRESS_LIMIT_REACHED";

    public static BusinessException InvalidCredentials() => new(
        InvalidCredentialsCode, "Email hoặc mật khẩu không đúng", StatusCodes.Status401Unauthorized);

    public static BusinessException AccountLocked() => new(
        AccountLockedCode, "Tài khoản tạm khóa do đăng nhập sai nhiều lần, vui lòng thử lại sau 15 phút",
        StatusCodes.Status423Locked);

    public static BusinessException EmailTaken() =>
        new(EmailTakenCode, "Email đã được đăng ký", StatusCodes.Status409Conflict);

    public static BusinessException InvalidRefreshToken() => new(
        InvalidRefreshTokenCode, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại",
        StatusCodes.Status401Unauthorized);

    public static BusinessException TwoFactorTokenInvalid() => new(
        TwoFactorTokenInvalidCode, "Phiên xác thực 2 bước đã hết hạn, vui lòng đăng nhập lại",
        StatusCodes.Status401Unauthorized);

    public static BusinessException OtpInvalid() =>
        new(OtpInvalidCode, "Mã OTP không đúng hoặc đã hết hạn", StatusCodes.Status400BadRequest);

    public static BusinessException TwoFactorAlreadyEnabled() => new(
        TwoFactorAlreadyEnabledCode, "Tài khoản đã bật xác thực 2 bước", StatusCodes.Status409Conflict);

    public static BusinessException UserNotFound() =>
        new(UserNotFoundCode, "Không tìm thấy người dùng", StatusCodes.Status404NotFound);

    public static BusinessException RoleInvalid(string role) => new(RoleInvalidCode, $"Vai trò không hợp lệ: {role}");

    public static BusinessException CannotRemoveOwnAdmin() => new(
        CannotRemoveOwnAdminCode, "Không thể tự gỡ quyền Super Admin của chính mình");

    public static BusinessException CannotImpersonate() => new(
        CannotImpersonateCode, "Không thể đăng nhập nhanh vào chính mình hoặc tài khoản Super Admin khác");

    public static BusinessException AddressNotFound() =>
        new(AddressNotFoundCode, "Không tìm thấy địa chỉ", StatusCodes.Status404NotFound);

    public static BusinessException AddressLimitReached(int max) =>
        new(AddressLimitReachedCode, $"Sổ địa chỉ tối đa {max} địa chỉ, vui lòng xóa bớt địa chỉ cũ");

    public static BusinessException IdentityFailed(IEnumerable<string> errors) =>
        new(IdentityErrorCode, string.Join("; ", errors));
}

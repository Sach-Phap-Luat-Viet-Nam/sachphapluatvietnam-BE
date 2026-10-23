using FluentValidation;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

internal static class AuthRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    // Số di động Việt Nam: 0xxxxxxxxx hoặc +84xxxxxxxxx.
    public const string PhonePattern = @"^(0|\+84)[0-9]{9}$";

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(PasswordMinLength, PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(256);

    public static IRuleBuilderOptions<T, string> ValidOtp<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^[0-9]{6}$").WithMessage("Mã OTP gồm 6 chữ số");
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
        RuleFor(request => request.Password).ValidPassword();
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(255);
        RuleFor(request => request.PhoneNumber).Matches(AuthRules.PhonePattern)
            .When(request => !string.IsNullOrEmpty(request.PhoneNumber));
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(AuthRules.PasswordMaxLength);
    }
}

internal sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(request => request.TwoFactorToken).NotEmpty();
        RuleFor(request => request.Code).ValidOtp();
    }
}

internal sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator() => RuleFor(request => request.Code).ValidOtp();
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty();
        RuleFor(request => request.NewPassword).ValidPassword();
    }
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(255);
        RuleFor(request => request.PhoneNumber).Matches(AuthRules.PhonePattern)
            .When(request => !string.IsNullOrEmpty(request.PhoneNumber));
        RuleFor(request => request.Occupation).IsInEnum();
    }
}

internal sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Password).ValidPassword();
        RuleFor(request => request.Roles).NotEmpty();
        RuleForEach(request => request.Roles).Must(Roles.Staff.Contains).WithMessage("Vai trò nhân viên không hợp lệ");
    }
}

internal sealed class SetRolesRequestValidator : AbstractValidator<SetRolesRequest>
{
    public SetRolesRequestValidator() =>
        RuleForEach(request => request.Roles).Must(Roles.All.Contains).WithMessage("Vai trò không hợp lệ");
}

using AwesomeAssertions;
using SachLuat.Modules.Auth.Application;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class AdminUserValidatorTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("mat-khau-moi-rat-dai")]
    public void ResetPassword_Valid_IsValid(string password)
    {
        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(password)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public void ResetPassword_TooShort_IsInvalid(string password)
    {
        var result = new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(password));

        result.Errors.Should().Contain(error => error.PropertyName == "NewPassword");
    }

    [Fact]
    public void ResetPassword_TooLong_IsInvalid()
    {
        var password = new string('a', AuthRules.PasswordMaxLength + 1);

        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(password)).IsValid.Should().BeFalse();
    }
}

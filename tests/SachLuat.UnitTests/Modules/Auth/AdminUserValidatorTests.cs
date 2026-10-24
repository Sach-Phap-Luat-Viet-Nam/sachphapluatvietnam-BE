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

    [Fact]
    public void SetRoles_SuperAdmin_IsInvalid()
    {
        new SetRolesRequestValidator().Validate(new SetRolesRequest(["marketing", "super_admin"])).IsValid
            .Should().BeFalse();
        new SetRolesRequestValidator().Validate(new SetRolesRequest(["customer", "marketing"])).IsValid
            .Should().BeTrue();
    }

    [Fact]
    public void CreateStaff_SuperAdmin_IsInvalid()
    {
        var request = new CreateStaffRequest("sa@example.com", "Super Admin", "mat-khau-tam-1", ["super_admin"]);

        new CreateStaffRequestValidator().Validate(request).IsValid.Should().BeFalse();
        new CreateStaffRequestValidator().Validate(request with { Roles = ["accountant"] }).IsValid.Should().BeTrue();
    }
}

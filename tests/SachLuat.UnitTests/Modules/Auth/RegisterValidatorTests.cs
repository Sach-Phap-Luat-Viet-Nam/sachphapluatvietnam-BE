using AwesomeAssertions;
using SachLuat.Modules.Auth.Application;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class RegisterValidatorTests
{
    [Fact]
    public void Register_AcceptedTerms_IsValid()
    {
        var result = new RegisterRequestValidator().Validate(Register(acceptTerms: true));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Register_NotAcceptedTerms_IsInvalid()
    {
        var result = new RegisterRequestValidator().Validate(Register(acceptTerms: false));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "AcceptTerms");
    }

    private static RegisterRequest Register(bool acceptTerms) =>
        new("khach@example.com", "mat-khau-an-toan", "Nguyễn Văn A", "0901234567", acceptTerms);
}

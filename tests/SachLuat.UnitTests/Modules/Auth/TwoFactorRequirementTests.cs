using System.Security.Claims;
using AwesomeAssertions;
using SachLuat.Modules.Auth;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class TwoFactorRequirementTests
{
    [Theory]
    [InlineData(AuthClaimTypes.MultiFactor, new[] { Roles.SuperAdmin }, true)]
    [InlineData(AuthClaimTypes.Password, new[] { Roles.SuperAdmin }, false)]
    [InlineData(AuthClaimTypes.Password, new[] { Roles.ProductManager, Roles.SuperAdmin }, false)]
    [InlineData(AuthClaimTypes.Password, new[] { Roles.WarehouseKeeper }, true)]
    [InlineData(AuthClaimTypes.Password, new[] { Roles.OrderManager, Roles.Accountant }, true)]
    [InlineData(AuthClaimTypes.MultiFactor, new[] { Roles.CustomerService }, true)]
    public void HasRequiredTwoFactor_OnlySuperAdminNeedsMultiFactor(string amr, string[] roles, bool expected)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role))
            .Append(new Claim(AuthClaimTypes.AuthenticationMethod, amr));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        AuthModule.HasRequiredTwoFactor(user).Should().Be(expected);
    }

    [Theory]
    [InlineData(new[] { Roles.SuperAdmin }, false, true)]
    [InlineData(new[] { Roles.SuperAdmin }, true, false)]
    [InlineData(new[] { Roles.WarehouseKeeper }, false, false)]
    [InlineData(new[] { Roles.Customer }, false, false)]
    public void ToResponse_RequiresTwoFactorSetupOnlyForSuperAdminWithoutTwoFactor(
        string[] roles,
        bool twoFactorEnabled,
        bool expected)
    {
        var user = new AppUser { Email = "nv@example.com", FullName = "Nhân viên", TwoFactorEnabled = twoFactorEnabled };

        var response = AuthService.ToResponse(user, roles);

        response.RequiresTwoFactorSetup.Should().Be(expected);
        response.TwoFactorEnabled.Should().Be(twoFactorEnabled);
    }
}

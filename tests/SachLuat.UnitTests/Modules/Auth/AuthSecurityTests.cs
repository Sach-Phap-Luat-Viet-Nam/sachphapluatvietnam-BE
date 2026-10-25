using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class AuthSecurityTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-with-at-least-32-chars",
    };

    [Fact]
    public void BCryptHasher_HashesWithBcryptAndVerifies()
    {
        var hasher = new BCryptPasswordHasher();
        var user = new AppUser();

        var hash = hasher.HashPassword(user, "mat-khau-an-toan");

        hash.Should().StartWith("$2");
        hasher.VerifyHashedPassword(user, hash, "mat-khau-an-toan").Should().Be(PasswordVerificationResult.Success);
        hasher.VerifyHashedPassword(user, hash, "sai-mat-khau").Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public async Task TwoFactorTicket_RoundTrips()
    {
        var service = new TokenService(Options.Create(Jwt), TimeProvider.System);

        var ticket = service.CreateTwoFactorTicket(userId: 42, rememberMe: true);
        var result = await service.ValidateTwoFactorTicketAsync(ticket);

        result.Should().Be(new TwoFactorTicket(42, RememberMe: true));
    }

    [Fact]
    public async Task TwoFactorTicket_Expired_IsRejected()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-10));
        var service = new TokenService(Options.Create(Jwt), clock);

        var ticket = service.CreateTwoFactorTicket(userId: 42, rememberMe: false);

        (await new TokenService(Options.Create(Jwt), TimeProvider.System).ValidateTwoFactorTicketAsync(ticket))
            .Should().BeNull();
    }

    [Fact]
    public async Task AccessToken_IsNotAcceptedAsTwoFactorTicket()
    {
        var service = new TokenService(Options.Create(Jwt), TimeProvider.System);
        var accessToken = service.CreateAccessToken(
            new AppUser { Id = 42, Email = "a@b.vn" }, ["customer"], isMultiFactor: false, sessionVersion: 0);

        (await service.ValidateTwoFactorTicketAsync(accessToken.Token)).Should().BeNull();
    }

    [Fact]
    public void AccessToken_SelfLogin_IsMarkedPortalWithoutImpersonator()
    {
        var service = new TokenService(Options.Create(Jwt), TimeProvider.System);

        var token = new JsonWebToken(
            service.CreateAccessToken(
                new AppUser { Id = 42, Email = "a@b.vn" }, ["customer"], isMultiFactor: false, sessionVersion: 0)
            .Token);

        token.GetClaim(AuthClaimTypes.LoginVia).Value.Should().Be(AuthClaimTypes.LoginViaPortal);
        token.TryGetClaim(AuthClaimTypes.ImpersonatorId, out _).Should().BeFalse();
    }

    [Fact]
    public void AccessToken_Impersonated_IsMarkedAdminWithImpersonator()
    {
        var service = new TokenService(Options.Create(Jwt), TimeProvider.System);

        var token = new JsonWebToken(service.CreateAccessToken(
            new AppUser { Id = 42, Email = "a@b.vn" },
            ["warehouse_keeper"],
            isMultiFactor: true,
            sessionVersion: 3,
            impersonatorId: 1).Token);

        token.GetClaim(AuthClaimTypes.Subject).Value.Should().Be("42");
        token.GetClaim(AuthClaimTypes.LoginVia).Value.Should().Be(AuthClaimTypes.LoginViaAdmin);
        token.GetClaim(AuthClaimTypes.ImpersonatorId).Value.Should().Be("1");
        token.GetClaim(AuthClaimTypes.SessionVersion).Value.Should().Be("3");
        token.GetClaim(AuthClaimTypes.AuthenticationMethod).Value.Should().Be(AuthClaimTypes.MultiFactor);
    }

    [Fact]
    public void TrustedDevice_IsActiveOnlyBeforeExpiry()
    {
        var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var device = new TrustedDevice(userId: 42, tokenHash: "hash", created, created.AddDays(30));

        device.IsActive(created.AddDays(29)).Should().BeTrue();
        device.IsActive(created.AddDays(30)).Should().BeFalse();
        device.IsActive(created.AddDays(31)).Should().BeFalse();
    }

    [Fact]
    public void TwoFactorLoginRequestValidator_AcceptsTrustDeviceFlag()
    {
        var validator = new TwoFactorLoginRequestValidator();

        validator.Validate(new TwoFactorLoginRequest("ticket", "123456", TrustDevice: true)).IsValid
            .Should().BeTrue();
        validator.Validate(new TwoFactorLoginRequest("ticket", "12345")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_HashIsDeterministicAndDiffersFromToken()
    {
        var (token, hash) = TokenService.CreateRefreshToken();

        hash.Should().HaveLength(64).And.NotBe(token);
        TokenService.HashRefreshToken(token).Should().Be(hash);
    }
}

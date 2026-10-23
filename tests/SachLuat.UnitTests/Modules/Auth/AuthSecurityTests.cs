using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure;
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
        var accessToken = service.CreateAccessToken(new AppUser { Id = 42, Email = "a@b.vn" }, ["customer"], false);

        (await service.ValidateTwoFactorTicketAsync(accessToken.Token)).Should().BeNull();
    }

    [Fact]
    public void RefreshToken_HashIsDeterministicAndDiffersFromToken()
    {
        var (token, hash) = TokenService.CreateRefreshToken();

        hash.Should().HaveLength(64).And.NotBe(token);
        TokenService.HashRefreshToken(token).Should().Be(hash);
    }
}

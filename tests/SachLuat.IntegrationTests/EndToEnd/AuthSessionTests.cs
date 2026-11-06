using System.Net;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

[Collection(EndToEndCollection.Name)]
public sealed class AuthSessionTests(EndToEndFactory factory)
{
    [Fact]
    public async Task RefreshRotatesTokenAndLogoutRevokesSession()
    {
        factory.SkipIfNotConfigured();
        using var client = new ApiClient(factory);
        await client.PostAsync("/api/v1/auth/register", new
        {
            email = "session@e2e.test",
            password = "session-password",
            fullName = "Phiên đăng nhập",
            acceptTerms = true,
        }, HttpStatusCode.Created);

        // Cookie HttpOnly sl_refresh được gửi tự động; mỗi lần refresh cấp token mới.
        var refreshed = await client.PostAsync("/api/v1/auth/refresh");
        refreshed.GetProperty("user").GetProperty("email").GetString().Should().Be("session@e2e.test");

        await client.PostAsync("/api/v1/auth/logout", expected: HttpStatusCode.NoContent);
        (await client.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task FiveWrongPasswords_LocksAccount()
    {
        factory.SkipIfNotConfigured();
        using var client = new ApiClient(factory);
        await client.PostAsync("/api/v1/auth/register", new
        {
            email = "lock@e2e.test",
            password = "correct-password",
            fullName = "Khóa tài khoản",
            acceptTerms = true,
        }, HttpStatusCode.Created);

        for (var attempt = 1; attempt < 5; attempt++)
        {
            await client.PostAsync("/api/v1/auth/login",
                new { email = "lock@e2e.test", password = "wrong-password" }, HttpStatusCode.Unauthorized);
        }

        // Lần sai thứ 5 khóa tài khoản (FR-03); mật khẩu đúng cũng bị từ chối trong 15 phút.
        await client.PostAsync("/api/v1/auth/login",
            new { email = "lock@e2e.test", password = "wrong-password" }, HttpStatusCode.Locked);
        (await client.PostAsync("/api/v1/auth/login",
                new { email = "lock@e2e.test", password = "correct-password" }, HttpStatusCode.Locked))
            .GetProperty("code").GetString().Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Register_RequiresTermsAndStoresAcceptance()
    {
        factory.SkipIfNotConfigured();
        using var client = new ApiClient(factory);
        var request = new Dictionary<string, object>
        {
            ["email"] = "terms@e2e.test",
            ["password"] = "terms-password",
            ["fullName"] = "Đồng ý điều khoản",
        };

        // Thiếu hoặc false đều bị từ chối, chưa tạo tài khoản.
        (await client.PostAsync("/api/v1/auth/register", request, HttpStatusCode.BadRequest))
            .GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        request["acceptTerms"] = false;
        await client.PostAsync("/api/v1/auth/register", request, HttpStatusCode.BadRequest);

        request["acceptTerms"] = true;
        var registered = await client.PostAsync("/api/v1/auth/register", request, HttpStatusCode.Created);
        var userId = registered.GetProperty("user").GetProperty("id").GetInt64();

        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users
            .SingleAsync(candidate => candidate.Id == userId, TestContext.Current.CancellationToken);
        user.TermsAcceptedAt.Should().NotBeNull();
        user.TermsVersion.Should().Be(scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>()
            .Value.TermsVersion);
    }
}

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Mỗi tài khoản một nơi đăng nhập; buộc đăng xuất có hiệu lực ngay và báo qua kênh realtime.</summary>
[Collection(EndToEndCollection.Name)]
public sealed class SingleSessionTests(EndToEndFactory factory)
{
    private const string RootEmail = "root-session@e2e.test";
    private const string RootPassword = "root-session-password";

    [Fact]
    public async Task SecondLogin_EndsFirstSessionImmediately_AndNotifiesIt()
    {
        factory.SkipIfNotConfigured();
        using var first = new ApiClient(factory);
        var registered = await first.PostAsync("/api/v1/auth/register", new
        {
            email = "mot-noi@e2e.test",
            password = "mot-noi-password",
            fullName = "Một nơi",
        }, HttpStatusCode.Created);
        first.AccessToken = registered.GetProperty("accessToken").GetString();
        var userId = registered.GetProperty("user").GetProperty("id").GetInt64();
        await first.GetAsync("/api/v1/me");
        factory.SessionNotices.For(userId).Should().BeEmpty("lần đăng ký đầu không có phiên nào để đá");

        using var second = new ApiClient(factory);
        var secondSession = await second.LoginAsync("mot-noi@e2e.test", "mot-noi-password");

        // Access token cũ (chưa hết hạn) bị từ chối ngay, refresh token cũ cũng bị thu hồi.
        (await first.GetAsync("/api/v1/me", HttpStatusCode.Unauthorized)).GetProperty("code").GetString()
            .Should().Be("UNAUTHORIZED");
        (await first.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        (await second.GetAsync("/api/v1/me")).GetProperty("email").GetString().Should().Be("mot-noi@e2e.test");

        // Thiết bị mới mang đúng phiên bản trong thông báo nên bỏ qua; thiết bị cũ có phiên bản thấp hơn.
        var notice = factory.SessionNotices.For(userId).Should().ContainSingle().Subject;
        notice.Reason.Should().Be(SessionEndReason.SignedInElsewhere);
        notice.Version.Should().Be(secondSession.GetProperty("sessionVersion").GetInt32());
        notice.Version.Should().BeGreaterThan(registered.GetProperty("sessionVersion").GetInt32());
    }

    [Fact]
    public async Task ForceLogout_EndsAccessTokenImmediately_AndNotifiesEvenWithoutRefreshToken()
    {
        factory.SkipIfNotConfigured();
        await SeedSuperAdminAsync();
        using var admin = new ApiClient(factory);
        await admin.LoginAsync(RootEmail, RootPassword, portal: "admin");
        await admin.EnableTwoFactorAsync();

        using var victim = new ApiClient(factory);
        var registered = await victim.PostAsync("/api/v1/auth/register", new
        {
            email = "bi-day@e2e.test",
            password = "bi-day-password",
            fullName = "Bị buộc đăng xuất",
        }, HttpStatusCode.Created);
        victim.AccessToken = registered.GetProperty("accessToken").GetString();
        var victimId = registered.GetProperty("user").GetProperty("id").GetInt64();
        await victim.GetAsync("/api/v1/me");

        (await admin.PostAsync($"/api/v1/admin/users/{victimId}/logout"))
            .GetProperty("revokedSessions").GetInt32().Should().Be(1);
        (await victim.GetAsync("/api/v1/me", HttpStatusCode.Unauthorized)).GetProperty("code").GetString()
            .Should().Be("UNAUTHORIZED");
        var notice = factory.SessionNotices.For(victimId).Should().ContainSingle().Subject;
        notice.Reason.Should().Be(SessionEndReason.ForcedLogout);

        // Không còn phiên nào nhưng vẫn báo: thiết bị có thể chỉ còn access token.
        (await admin.PostAsync($"/api/v1/admin/users/{victimId}/logout"))
            .GetProperty("revokedSessions").GetInt32().Should().Be(0);
        factory.SessionNotices.For(victimId).Should().HaveCount(2);
    }

    [Fact]
    public async Task RealtimeAuth_RejectsChannelsOfOtherAccounts_AndIsUnavailableWithoutPusher()
    {
        factory.SkipIfNotConfigured();
        using var client = new ApiClient(factory);
        var registered = await client.PostAsync("/api/v1/auth/register", new
        {
            email = "realtime@e2e.test",
            password = "realtime-password",
            fullName = "Realtime",
        }, HttpStatusCode.Created);
        client.AccessToken = registered.GetProperty("accessToken").GetString();
        var userId = registered.GetProperty("user").GetProperty("id").GetInt64();

        // Môi trường test không cấu hình Pusher nên API ký kênh báo chưa bật.
        (await client.PostAsync("/api/v1/realtime/auth",
                new { socketId = "123.456", channelName = $"private-user-{userId}" },
                HttpStatusCode.ServiceUnavailable))
            .GetProperty("code").GetString().Should().Be("REALTIME_UNAVAILABLE");
    }

    private async Task SeedSuperAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(RootEmail) is not null)
        {
            return;
        }

        var user = new AppUser { UserName = RootEmail, Email = RootEmail, FullName = "Root session" };
        (await users.CreateAsync(user, RootPassword)).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, Roles.SuperAdmin)).Succeeded.Should().BeTrue();
    }
}

using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Thiết bị tin cậy: nhập mã 2FA một lần rồi đăng nhập lại bằng mật khẩu không bị hỏi mã tới khi hết hạn.</summary>
[Collection(EndToEndCollection.Name)]
public sealed class TrustedDeviceTests(EndToEndFactory factory)
{
    private const string Password = "trusted-device-password";

    [Fact]
    public async Task TrustedDevice_SkipsTwoFactor_OnlyOnThatDevice_UntilExpiry()
    {
        factory.SkipIfNotConfigured();
        const string email = "tin-cay@e2e.test";
        var (userId, key) = await SeedStaffWithTwoFactorAsync(email, Roles.ProductManager);

        using var device = new ApiClient(factory);
        await device.LoginAsync(email, Password, key, portal: "admin", trustDevice: true);

        // Đăng nhập lại (kể cả nhiều lần, đá phiên cũ) không hỏi mã và vẫn qua được policy cần amr=mfa.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var login = await LoginWithPasswordAsync(device, email);
            login.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
            device.AccessToken = login.GetProperty("session").GetProperty("accessToken").GetString();
            await device.GetAsync("/api/v1/admin/books");
        }

        // Thiết bị khác không có cookie vẫn bị hỏi mã.
        using var otherDevice = new ApiClient(factory);
        (await LoginWithPasswordAsync(otherDevice, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeTrue();

        await ExpireTrustedDevicesAsync(userId);
        (await LoginWithPasswordAsync(device, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task WithoutTrustDevice_EveryLoginAsksForCode()
    {
        factory.SkipIfNotConfigured();
        const string email = "khong-nho@e2e.test";
        var (_, key) = await SeedStaffWithTwoFactorAsync(email, Roles.ProductManager);

        using var device = new ApiClient(factory);
        await device.LoginAsync(email, Password, key, portal: "admin");

        (await LoginWithPasswordAsync(device, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task ForceLogout_RevokesTrustedDevices()
    {
        factory.SkipIfNotConfigured();
        const string email = "bi-thu-hoi@e2e.test";
        var (userId, key) = await SeedStaffWithTwoFactorAsync(email, Roles.ProductManager);
        var (_, rootKey) = await SeedStaffWithTwoFactorAsync("root-trust-revoke@e2e.test", Roles.SuperAdmin);
        using var device = new ApiClient(factory);
        await device.LoginAsync(email, Password, key, portal: "admin", trustDevice: true);
        (await LoginWithPasswordAsync(device, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeFalse();

        using var admin = new ApiClient(factory);
        await admin.LoginAsync("root-trust-revoke@e2e.test", Password, rootKey, portal: "admin");
        await admin.PostAsync($"/api/v1/admin/users/{userId}/logout");

        // Cookie vẫn còn trên thiết bị nhưng bản ghi đã bị xóa phía server.
        (await LoginWithPasswordAsync(device, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task SuperAdminInConfig_IsTrusted_ButOtherSuperAdminsAreNot()
    {
        factory.SkipIfNotConfigured();
        const string email = EndToEndFactory.TrustedSuperAdminEmail;
        var (_, key) = await SeedStaffWithTwoFactorAsync(email, Roles.SuperAdmin);

        using var device = new ApiClient(factory);
        await device.LoginAsync(email, Password, key, portal: "admin", trustDevice: true);

        var login = await LoginWithPasswordAsync(device, email);
        login.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
        device.AccessToken = login.GetProperty("session").GetProperty("accessToken").GetString();
        await device.GetAsync("/api/v1/admin/users");
    }

    [Fact]
    public async Task SuperAdminNotInConfig_IsNeverTrusted_EvenWhenTickingTrustDevice()
    {
        factory.SkipIfNotConfigured();
        const string email = "root-khong-nho@e2e.test";
        var (_, key) = await SeedStaffWithTwoFactorAsync(email, Roles.SuperAdmin);

        using var device = new ApiClient(factory);
        await device.LoginAsync(email, Password, key, portal: "admin", trustDevice: true);

        (await LoginWithPasswordAsync(device, email)).GetProperty("requiresTwoFactor").GetBoolean()
            .Should().BeTrue();
    }

    private static Task<JsonElement> LoginWithPasswordAsync(ApiClient client, string email) =>
        client.PostAsync("/api/v1/auth/login", new { email, password = Password, portal = "admin" });

    /// <summary>Tạo tài khoản có vai trò <paramref name="role"/> và bật 2FA; trả id và khóa TOTP.</summary>
    private async Task<(long UserId, string Key)> SeedStaffWithTwoFactorAsync(string email, string role)
    {
        long userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Thiết bị tin cậy" };
            (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        using var setup = new ApiClient(factory);
        await setup.LoginAsync(email, Password, portal: "admin");
        return (userId, await setup.EnableTwoFactorAsync());
    }

    private async Task ExpireTrustedDevicesAsync(long userId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var updated = await dbContext.TrustedDevices
            .Where(device => device.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(device => device.ExpiresAt, device => device.CreatedAt.AddDays(-1)),
                TestContext.Current.CancellationToken);
        updated.Should().BeGreaterThan(0);
    }
}

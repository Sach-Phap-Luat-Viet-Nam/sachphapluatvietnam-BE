using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Super Admin quản lý khách hàng và tài khoản dưới cấp: tìm kiếm, khóa/mở khóa, đặt lại mật khẩu.</summary>
[Collection(EndToEndCollection.Name)]
public sealed class AdminUsersTests(EndToEndFactory factory)
{
    private const string RootEmail = "root-users@e2e.test";
    private const string RootPassword = "root-users-password";
    private const string CustomerEmail = "khach-quan-tri@e2e.test";
    private const string CustomerPassword = "khach-password-1";

    [Fact]
    public async Task SuperAdmin_ManagesCustomers()
    {
        factory.SkipIfNotConfigured();
        // Super Admin riêng cho test này: bootstrap admin được PurchaseFlowTests dùng để kiểm tra bước bật 2FA.
        await SeedSuperAdminAsync();
        using var admin = new ApiClient(factory);
        var adminSession = await admin.LoginAsync(RootEmail, RootPassword);
        var adminId = adminSession.GetProperty("user").GetProperty("id").GetInt64();
        await admin.EnableTwoFactorAsync();

        using var customer = new ApiClient(factory);
        var registered = await customer.PostAsync("/api/v1/auth/register", new
        {
            email = CustomerEmail,
            password = CustomerPassword,
            fullName = "Khách cần hỗ trợ",
            phoneNumber = "0912345678",
        }, HttpStatusCode.Created);
        customer.AccessToken = registered.GetProperty("accessToken").GetString();
        var customerId = registered.GetProperty("user").GetProperty("id").GetInt64();
        await customer.PostAsync("/api/v1/me/addresses", new
        {
            recipientName = "Khách cần hỗ trợ",
            phone = "0912345678",
            province = "Hà Nội",
            ward = "Phường Cửa Nam",
            streetAddress = "12 Hàng Bài",
            isDefault = true,
        }, HttpStatusCode.Created);

        // Danh sách khách tìm theo số điện thoại; danh sách nhân viên không chứa khách.
        var customers = await admin.GetAsync("/api/v1/admin/customers?q=0912345678");
        customers.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
        var row = customers.GetProperty("items")[0];
        row.GetProperty("email").GetString().Should().Be(CustomerEmail);
        row.GetProperty("isLocked").GetBoolean().Should().BeFalse();
        Emails(await admin.GetAsync("/api/v1/admin/users?limit=100")).Should().NotContain(CustomerEmail);
        Emails(await admin.GetAsync($"/api/v1/admin/users?q={Uri.EscapeDataString(RootEmail)}"))
            .Should().Equal(RootEmail);
        // Ký tự wildcard của LIKE được escape, không khớp mọi dòng.
        (await admin.GetAsync("/api/v1/admin/customers?q=%25")).GetProperty("meta").GetProperty("total").GetInt32()
            .Should().Be(0);

        var detail = await admin.GetAsync($"/api/v1/admin/customers/{customerId}");
        detail.GetProperty("account").GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Equal("customer");
        detail.GetProperty("addresses").GetArrayLength().Should().Be(1);
        detail.GetProperty("invoiceInfo").ValueKind.Should().Be(JsonValueKind.Null);
        (await admin.GetAsync($"/api/v1/admin/customers/{adminId}", HttpStatusCode.NotFound))
            .GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");

        // Khóa: thu hồi phiên đang có, đăng nhập bị từ chối với mã riêng (không phải tạm khóa 15 phút).
        var locked = await admin.PostAsync($"/api/v1/admin/users/{customerId}/lock");
        locked.GetProperty("isLocked").GetBoolean().Should().BeTrue();
        locked.GetProperty("lockedUntil").ValueKind.Should().Be(JsonValueKind.Null);
        (await customer.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        (await customer.PostAsync("/api/v1/auth/login",
                new { email = CustomerEmail, password = CustomerPassword }, HttpStatusCode.Forbidden))
            .GetProperty("code").GetString().Should().Be("ACCOUNT_DISABLED");

        (await admin.PostAsync($"/api/v1/admin/users/{customerId}/unlock")).GetProperty("isLocked").GetBoolean()
            .Should().BeFalse();
        await customer.LoginAsync(CustomerEmail, CustomerPassword);

        // Đặt lại mật khẩu: mật khẩu cũ hết dùng được, phiên cũ bị thu hồi.
        await admin.PostAsync($"/api/v1/admin/users/{customerId}/password/reset",
            new { newPassword = "short" }, HttpStatusCode.BadRequest);
        await admin.PostAsync($"/api/v1/admin/users/{customerId}/password/reset",
            new { newPassword = "mat-khau-tam-123" }, HttpStatusCode.NoContent);
        await customer.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized);
        await customer.PostAsync("/api/v1/auth/login",
            new { email = CustomerEmail, password = CustomerPassword }, HttpStatusCode.Unauthorized);
        await customer.LoginAsync(CustomerEmail, "mat-khau-tam-123");

        // Không khóa/đặt lại mật khẩu được chính mình hay Super Admin khác.
        var bootstrapAdminId = (await admin.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(EndToEndFactory.AdminEmail)}"))
            .GetProperty("items")[0].GetProperty("id").GetInt64();
        foreach (var targetId in new[] { adminId, bootstrapAdminId })
        {
            (await admin.PostAsync($"/api/v1/admin/users/{targetId}/lock",
                    expected: HttpStatusCode.UnprocessableEntity))
                .GetProperty("code").GetString().Should().Be("CANNOT_MANAGE_USER");
        }

        (await admin.PostAsync($"/api/v1/admin/users/{bootstrapAdminId}/password/reset",
                new { newPassword = "mat-khau-tam-123" }, HttpStatusCode.UnprocessableEntity))
            .GetProperty("code").GetString().Should().Be("CANNOT_MANAGE_USER");

        // Khách hàng không gọi được API quản trị.
        await customer.GetAsync("/api/v1/admin/customers", HttpStatusCode.Forbidden);
    }

    private async Task SeedSuperAdminAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = RootEmail, Email = RootEmail, FullName = "Super Admin quản lý user" };
        (await userManager.CreateAsync(user, RootPassword)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, Roles.SuperAdmin)).Succeeded.Should().BeTrue();
    }

    private static IEnumerable<string?> Emails(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("email").GetString());
}

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Super Admin tra cứu khách hàng, tìm nhân viên; Super Admin chỉ đăng nhập ở app Admin.</summary>
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
        // Super Admin không đăng nhập được ở Storefront (mặc định khi không gửi portal), kể cả đúng mật khẩu.
        (await admin.PostAsync("/api/v1/auth/login",
                new { email = RootEmail, password = RootPassword }, HttpStatusCode.Forbidden))
            .GetProperty("code").GetString().Should().Be("SUPER_ADMIN_PORTAL_FORBIDDEN");
        (await admin.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        var adminSession = await admin.LoginAsync(RootEmail, RootPassword, portal: "admin");
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
        const string avatarUrl = "https://example.com/khach-quan-tri.png";
        await customer.PutAsync("/api/v1/me/avatar", new { avatarUrl });

        // Danh sách khách tìm theo số điện thoại; danh sách nhân viên không chứa khách.
        var customers = await admin.GetAsync("/api/v1/admin/customers?q=0912345678");
        customers.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
        var row = customers.GetProperty("items")[0];
        row.GetProperty("email").GetString().Should().Be(CustomerEmail);
        row.GetProperty("isActive").GetBoolean().Should().BeTrue();
        row.GetProperty("avatarUrl").GetString().Should().Be(avatarUrl);
        Emails(await admin.GetAsync("/api/v1/admin/users?limit=100")).Should().NotContain(CustomerEmail);
        Emails(await admin.GetAsync($"/api/v1/admin/users?q={Uri.EscapeDataString(RootEmail)}"))
            .Should().Equal(RootEmail);
        // Ký tự wildcard của LIKE được escape, không khớp mọi dòng.
        (await admin.GetAsync("/api/v1/admin/customers?q=%25")).GetProperty("meta").GetProperty("total").GetInt32()
            .Should().Be(0);

        var detail = await admin.GetAsync($"/api/v1/admin/customers/{customerId}");
        detail.GetProperty("account").GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Equal("customer");
        detail.GetProperty("account").GetProperty("avatarUrl").GetString().Should().Be(avatarUrl);
        detail.GetProperty("addresses").GetArrayLength().Should().Be(1);
        detail.GetProperty("invoiceInfo").ValueKind.Should().Be(JsonValueKind.Null);
        (await admin.GetAsync($"/api/v1/admin/customers/{adminId}", HttpStatusCode.NotFound))
            .GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");

        // Vô hiệu hóa khách (API chung với nhân viên): thu hồi phiên, đăng nhập bị từ chối; kích hoạt lại thì vào được.
        (await admin.PostAsync($"/api/v1/admin/users/{customerId}/deactivate")).GetProperty("isActive").GetBoolean()
            .Should().BeFalse();
        (await customer.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        (await customer.PostAsync("/api/v1/auth/login",
                new { email = CustomerEmail, password = CustomerPassword }, HttpStatusCode.Forbidden))
            .GetProperty("code").GetString().Should().Be("ACCOUNT_DISABLED");
        (await admin.GetAsync("/api/v1/admin/customers?q=0912345678")).GetProperty("items")[0]
            .GetProperty("isActive").GetBoolean().Should().BeFalse();
        await admin.PostAsync($"/api/v1/admin/users/{customerId}/activate");
        await customer.LoginAsync(CustomerEmail, CustomerPassword);

        // Không đổi vai trò của chính mình hay Super Admin khác.
        var bootstrapAdminId = (await admin.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(EndToEndFactory.AdminEmail)}"))
            .GetProperty("items")[0].GetProperty("id").GetInt64();
        foreach (var targetId in new[] { adminId, bootstrapAdminId })
        {
            (await admin.PutAsync($"/api/v1/admin/users/{targetId}/roles",
                    new { roles = new[] { "marketing" } }, HttpStatusCode.UnprocessableEntity))
                .GetProperty("code").GetString().Should().Be("CANNOT_MANAGE_USER");
        }

        // Không cấp vai trò Super Admin qua API, kể cả khi tạo nhân viên mới.
        await admin.PutAsync($"/api/v1/admin/users/{customerId}/roles",
            new { roles = new[] { "customer", "super_admin" } }, HttpStatusCode.BadRequest);
        await admin.PostAsync("/api/v1/admin/users", new
        {
            email = "sa-moi@e2e.test",
            fullName = "Super Admin mới",
            password = "sa-moi-password-1",
            roles = new[] { "super_admin" },
        }, HttpStatusCode.BadRequest);

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

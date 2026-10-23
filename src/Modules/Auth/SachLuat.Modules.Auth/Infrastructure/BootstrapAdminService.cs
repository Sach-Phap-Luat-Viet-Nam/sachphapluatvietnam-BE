using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Infrastructure;

/// <summary>
/// Tạo Super Admin đầu tiên từ cấu hình <c>Auth:BootstrapAdmin</c> nếu email đó chưa tồn tại.
/// Admin này vẫn phải tự bật 2FA trước khi dùng API Admin.
/// </summary>
internal sealed class BootstrapAdminService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuthOptions> options,
    ILogger<BootstrapAdminService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrap = options.Value.BootstrapAdmin;
        if (string.IsNullOrWhiteSpace(bootstrap.Email) || string.IsNullOrWhiteSpace(bootstrap.Password))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            if (await userManager.FindByEmailAsync(bootstrap.Email) is not null)
            {
                return;
            }

            var admin = new AppUser { UserName = bootstrap.Email, Email = bootstrap.Email, FullName = bootstrap.FullName };
            AuthService.EnsureSucceeded(await userManager.CreateAsync(admin, bootstrap.Password));
            AuthService.EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.SuperAdmin));
            logger.LogInformation("Created bootstrap super admin {UserId}", admin.Id);
        }
        catch (Exception exception)
        {
            // Không chặn khởi động API (ví dụ CSDL chưa migrate); admin có thể tạo lại ở lần chạy sau.
            logger.LogError(exception, "Failed to create bootstrap super admin");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

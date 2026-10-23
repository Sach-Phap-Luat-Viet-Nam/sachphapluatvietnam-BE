using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SachLuat.SharedKernel.Modules;

/// <summary>
/// Điểm đăng ký của một module nghiệp vụ. Host gọi <see cref="AddServices"/> khi khởi động
/// và nạp controller trong assembly của module.
/// </summary>
public interface IModule
{
    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Áp migration của các DbContext thuộc module (chỉ gọi khi bật cấu hình, xem docs/setup.md).</summary>
    Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);
}

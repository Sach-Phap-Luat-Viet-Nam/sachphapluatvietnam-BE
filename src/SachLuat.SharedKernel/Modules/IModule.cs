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
}

using Microsoft.Extensions.Configuration;

namespace SachLuat.SharedKernel.Configuration;

public static class ConfigurationExtensions
{
    public const string DefaultConnectionName = "Default";

    /// <summary>
    /// Lấy connection string, báo lỗi ngay khi khởi động nếu chưa cấu hình thay vì lỗi lúc truy vấn.
    /// </summary>
    public static string GetRequiredConnectionString(
        this IConfiguration configuration,
        string name = DefaultConnectionName)
    {
        var connectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is not configured. See docs/setup.md.");
        }

        return connectionString;
    }
}

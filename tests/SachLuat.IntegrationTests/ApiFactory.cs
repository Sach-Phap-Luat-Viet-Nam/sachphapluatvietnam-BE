using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SachLuat.IntegrationTests;

/// <summary>Host không cần CSDL/Redis thật: chỉ kiểm tra pipeline (routing, lỗi, OpenAPI, phân quyền).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "integration-test-signing-key-at-least-32-chars";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Chuỗi giả để host khởi động được; các test ở đây không mở kết nối CSDL/Redis.
        builder.UseSetting(
            "ConnectionStrings:Default",
            "Host=localhost;Database=sachluat_test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6399");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Ordering:EnableExpiredPaymentSweep", "false");
    }
}

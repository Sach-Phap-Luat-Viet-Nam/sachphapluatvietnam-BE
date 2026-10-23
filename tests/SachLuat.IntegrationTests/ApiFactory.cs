using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SachLuat.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Chuỗi giả để host khởi động được; các test ở đây không mở kết nối CSDL.
        builder.UseSetting(
            "ConnectionStrings:Default",
            "Host=localhost;Database=sachluat_test;Username=test;Password=test");
    }
}

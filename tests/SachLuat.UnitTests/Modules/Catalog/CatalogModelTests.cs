using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class CatalogModelTests
{
    [Fact]
    public void Model_MatchesLatestMigration()
    {
        // Không mở kết nối: chỉ so model hiện tại với snapshot migration.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            })
            .Build();
        var services = new ServiceCollection();
        new CatalogModule().AddServices(services, configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        dbContext.Database.HasPendingModelChanges().Should().BeFalse(
            "đổi entity/cấu hình thì phải tạo migration mới (dotnet ef migrations add)");
    }
}

using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Auth;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.Modules.Catalog;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.Modules.Content;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.Modules.Inventory;
using SachLuat.Modules.Inventory.Infrastructure.Persistence;
using SachLuat.Modules.Ordering;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Modules;
using Xunit;

namespace SachLuat.UnitTests;

public sealed class ModuleModelTests
{
    public static TheoryData<string> Modules => [nameof(CatalogModule), nameof(AuthModule), nameof(InventoryModule),
        nameof(OrderingModule), nameof(ContentModule)];

    [Theory]
    [MemberData(nameof(Modules))]
    public void Model_MatchesLatestMigration(string moduleName)
    {
        // Không mở kết nối: chỉ so model hiện tại với snapshot migration của module.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            })
            .Build();
        var (module, contextType) = moduleName switch
        {
            nameof(CatalogModule) => ((IModule)new CatalogModule(), typeof(CatalogDbContext)),
            nameof(AuthModule) => (new AuthModule(), typeof(AuthDbContext)),
            nameof(InventoryModule) => (new InventoryModule(), typeof(InventoryDbContext)),
            nameof(ContentModule) => (new ContentModule(), typeof(ContentDbContext)),
            _ => (new OrderingModule(), typeof(OrderingDbContext)),
        };
        var services = new ServiceCollection();
        services.AddLogging();
        module.AddServices(services, configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

        dbContext.Database.HasPendingModelChanges().Should().BeFalse(
            "đổi entity/cấu hình thì phải tạo migration mới (dotnet ef migrations add)");
    }
}

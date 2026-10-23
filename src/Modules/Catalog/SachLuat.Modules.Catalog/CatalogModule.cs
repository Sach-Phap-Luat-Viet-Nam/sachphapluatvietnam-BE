using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;

namespace SachLuat.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString();

        services.AddDbContext<CatalogDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, CatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks()
            .AddDbContextCheck<CatalogDbContext>("catalog-db", tags: [HealthCheckTags.Ready]);
    }
}

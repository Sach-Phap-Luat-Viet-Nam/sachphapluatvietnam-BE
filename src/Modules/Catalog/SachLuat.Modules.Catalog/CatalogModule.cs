using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<CatalogDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, CatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddHealthChecks()
            .AddDbContextCheck<CatalogDbContext>("catalog-db", tags: [HealthCheckTags.Ready]);
    }
}

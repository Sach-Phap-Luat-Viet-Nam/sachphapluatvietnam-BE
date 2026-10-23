using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SachLuat.Modules.Inventory.Application;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Inventory.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Inventory;

public sealed class InventoryModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<InventoryDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, InventoryDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(provider => provider.GetRequiredService<InventoryService>());
        services.AddScoped<InventoryAdminService>();
        services.AddValidatorsFromAssembly(typeof(InventoryModule).Assembly, includeInternalTypes: true);

        services.AddHealthChecks()
            .AddDbContextCheck<InventoryDbContext>("inventory-db", tags: [HealthCheckTags.Ready]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync(cancellationToken);
    }
}

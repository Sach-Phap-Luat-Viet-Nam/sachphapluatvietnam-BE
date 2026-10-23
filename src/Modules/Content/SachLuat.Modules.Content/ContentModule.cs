using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Media;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Content;

public sealed class ContentModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<ContentDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, ContentDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<PopupService>();
        services.AddCloudinaryUploads(configuration);
        services.AddValidatorsFromAssembly(typeof(ContentModule).Assembly, includeInternalTypes: true);

        services.AddHealthChecks()
            .AddDbContextCheck<ContentDbContext>("content-db", tags: [HealthCheckTags.Ready]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.MigrateAsync(cancellationToken);
    }
}

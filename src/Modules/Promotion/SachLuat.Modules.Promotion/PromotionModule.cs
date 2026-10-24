using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SachLuat.Modules.Promotion.Application;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Promotion;

public sealed class PromotionModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PromotionOptions>()
            .Bind(configuration.GetSection(PromotionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetRequiredConnectionString();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<PromotionDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, PromotionDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<PromotionService>();
        services.AddScoped<IPromotionService>(provider => provider.GetRequiredService<PromotionService>());
        services.AddScoped<PromotionAdminService>();
        services.AddScoped<PromotionStorefrontService>();
        services.AddValidatorsFromAssembly(typeof(PromotionModule).Assembly, includeInternalTypes: true);

        services.AddHealthChecks()
            .AddDbContextCheck<PromotionDbContext>("promotion-db", tags: [HealthCheckTags.Ready]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync(cancellationToken);
    }
}

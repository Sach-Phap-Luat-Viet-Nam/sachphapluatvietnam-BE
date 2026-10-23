using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SachLuat.Modules.Ordering.Application;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering;

public sealed class OrderingModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OrderingOptions>()
            .Bind(configuration.GetSection(OrderingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetRequiredConnectionString();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<OrderingDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, OrderingDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<OrderCodeGenerator>();
        services.AddScoped<OrderStatusService>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderAdminService>();
        services.AddSingleton<ShippingFeeCalculator>();
        services.AddHostedService<ExpiredPaymentCancellationService>();
        services.AddValidatorsFromAssembly(typeof(OrderingModule).Assembly, includeInternalTypes: true);

        services.AddHealthChecks()
            .AddDbContextCheck<OrderingDbContext>("ordering-db", tags: [HealthCheckTags.Ready]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.MigrateAsync(cancellationToken);
    }
}

using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Cart.Application;
using SachLuat.Modules.Cart.Contracts;
using SachLuat.Modules.Cart.Infrastructure;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using StackExchange.Redis;

namespace SachLuat.Modules.Cart;

public sealed class CartModule : IModule
{
    public const string RedisConnectionName = "Redis";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CartOptions>()
            .Bind(configuration.GetSection(CartOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var redisConnectionString = configuration.GetRequiredConnectionString(RedisConnectionName);

        // Kết nối lần đầu khi cần; AbortOnConnectFail=false để Redis khởi động chậm không làm sập API.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddScoped<ICartStore, RedisCartStore>();
        services.AddScoped<CartKeyResolver>();
        services.AddScoped<CartService>();
        services.AddScoped<ICurrentCart>(provider => provider.GetRequiredService<CartService>());
        services.AddValidatorsFromAssembly(typeof(CartModule).Assembly, includeInternalTypes: true);

        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: [HealthCheckTags.Ready]);
    }

    // Giỏ hàng nằm trong Redis, không có migration.
    public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
}

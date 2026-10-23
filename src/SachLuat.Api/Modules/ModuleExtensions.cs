using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Modules;

namespace SachLuat.Api.Modules;

internal static class ModuleExtensions
{
    public static WebApplicationBuilder AddModules(this WebApplicationBuilder builder, params IModule[] modules)
    {
        var mvcBuilder = builder.Services
            .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
            .ConfigureApplicationPartManager(manager =>
                manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

        foreach (var module in modules)
        {
            builder.Services.AddSingleton(module);
            module.AddServices(builder.Services, builder.Configuration);
            mvcBuilder.AddApplicationPart(module.GetType().Assembly);
        }

        return builder;
    }

    public static async Task ApplyModuleMigrationsAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup)
        {
            return;
        }

        foreach (var module in app.Services.GetServices<IModule>())
        {
            await module.MigrateAsync(app.Services, app.Lifetime.ApplicationStopping);
        }
    }
}

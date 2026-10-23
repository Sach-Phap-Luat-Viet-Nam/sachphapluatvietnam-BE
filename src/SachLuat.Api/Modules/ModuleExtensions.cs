using SachLuat.SharedKernel.Modules;

namespace SachLuat.Api.Modules;

internal static class ModuleExtensions
{
    public static WebApplicationBuilder AddModules(this WebApplicationBuilder builder, params IModule[] modules)
    {
        var mvcBuilder = builder.Services.AddControllers();

        foreach (var module in modules)
        {
            module.AddServices(builder.Services, builder.Configuration);
            mvcBuilder.AddApplicationPart(module.GetType().Assembly);
        }

        return builder;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Notification.Infrastructure.Zalo;
using SachLuat.SharedKernel.Modules;

namespace SachLuat.Modules.Notification;

/// <summary>Kênh thông báo ra ngoài; hiện có tin nhắn nội bộ cho nhân viên qua Zalo Bot. Chưa có CSDL riêng.</summary>
public sealed class NotificationModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ZaloBotOptions>()
            .Bind(configuration.GetSection(ZaloBotOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !options.Enabled || options.IsConfigured,
                "Zalo:Enabled = true thì phải cấu hình Zalo:BotToken và Zalo:ChatId")
            .ValidateOnStart();

        services.AddHttpClient<IStaffNotifier, ZaloBotStaffNotifier>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ZaloBotOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            // Logger mặc định ghi URL request, mà URL của Zalo Bot API chứa token.
            .RemoveAllLoggers();
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
}

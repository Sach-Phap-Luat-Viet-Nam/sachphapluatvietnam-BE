using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Notification;
using SachLuat.Modules.Notification.Contracts;
using SachLuat.Modules.Notification.Infrastructure.Zalo;
using Xunit;

namespace SachLuat.UnitTests.Modules.Notification;

public sealed class NotificationModuleTests
{
    [Fact]
    public void AddServices_NoZaloSection_NotifierDisabled()
    {
        using var provider = BuildProvider([]);

        provider.GetRequiredService<IStaffNotifier>().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AddServices_ZaloConfigured_NotifierEnabled()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Zalo:Enabled"] = "true",
            ["Zalo:BotToken"] = "123:abc",
            ["Zalo:ChatId"] = "group-1",
        });

        provider.GetRequiredService<IStaffNotifier>().IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddServices_EnabledWithoutToken_FailsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Zalo:Enabled"] = "true" });

        var act = () => provider.GetRequiredService<IOptions<ZaloBotOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*Zalo:BotToken*");
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        new NotificationModule().AddServices(services, configuration);
        return services.BuildServiceProvider();
    }
}

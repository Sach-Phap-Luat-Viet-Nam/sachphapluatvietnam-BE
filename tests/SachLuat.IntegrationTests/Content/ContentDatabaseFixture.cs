using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SachLuat.Modules.Content;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Content;

/// <summary>
/// Chạy migration Content lên CSDL thật trỏ bởi <see cref="ConnectionStringVariable"/>; thiếu biến thì skip.
/// Đồng hồ giả cố định để lịch chạy popup trong test không phụ thuộc ngày chạy.
/// </summary>
public sealed class ContentDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "SACHLUAT_TEST_DB";

    private ServiceProvider? _serviceProvider;

    public string? ConnectionString { get; } = Environment.GetEnvironmentVariable(ConnectionStringVariable);

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 3, 5, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        new ContentModule().AddServices(services, configuration);
        services.AddSingleton<TimeProvider>(Clock);
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Database test dùng chung giữa các lần chạy: xóa hết popup cũ (kể cả đã xóa mềm) để "popup đang chạy"
    /// chỉ là của test hiện tại.
    /// </summary>
    internal async Task<AsyncServiceScope> CreateCleanScopeAsync()
    {
        Assert.SkipWhen(
            _serviceProvider is null,
            $"Set {ConnectionStringVariable} to run database tests (see docs/setup.md).");
        var scope = _serviceProvider!.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Popups
            .IgnoreQueryFilters()
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

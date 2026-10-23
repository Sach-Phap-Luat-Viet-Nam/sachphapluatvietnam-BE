using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

/// <summary>
/// Chạy migration Catalog lên CSDL thật trỏ bởi biến môi trường <see cref="ConnectionStringVariable"/>
/// (nên dùng database riêng, ví dụ <c>sachluat_test</c>). Không có biến thì các test bị skip.
/// </summary>
public sealed class CatalogDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "SACHLUAT_TEST_DB";

    private ServiceProvider? _serviceProvider;

    public string? ConnectionString { get; } = Environment.GetEnvironmentVariable(ConnectionStringVariable);

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
        new CatalogModule().AddServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    }

    internal AsyncServiceScope CreateScope()
    {
        Assert.SkipWhen(
            _serviceProvider is null,
            $"Set {ConnectionStringVariable} to run database tests (see docs/setup.md).");
        return _serviceProvider!.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

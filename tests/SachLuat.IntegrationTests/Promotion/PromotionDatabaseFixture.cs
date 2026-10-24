using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SachLuat.Modules.Promotion;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Promotion;

/// <summary>
/// Chạy migration Promotion lên CSDL thật trỏ bởi <see cref="ConnectionStringVariable"/>; thiếu biến thì skip.
/// Đồng hồ giả cố định để lịch chạy khuyến mãi trong test không phụ thuộc ngày chạy.
/// </summary>
public sealed class PromotionDatabaseFixture : IAsyncLifetime
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
        new PromotionModule().AddServices(services, configuration);
        services.AddSingleton<TimeProvider>(Clock);
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Database test dùng chung giữa các lần chạy và giữa các class: xóa sạch dữ liệu khuyến mãi (kể cả đã xóa mềm)
    /// để "chương trình đang chạy" chỉ là của test hiện tại. Các test dùng fixture này không chạy song song với nhau
    /// vì cùng nằm trong một collection.
    /// </summary>
    internal async Task<AsyncServiceScope> CreateCleanScopeAsync()
    {
        Assert.SkipWhen(
            _serviceProvider is null,
            $"Set {ConnectionStringVariable} to run database tests (see docs/setup.md).");
        var scope = _serviceProvider!.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        await dbContext.Redemptions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Coupons.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Promotions.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        return scope;
    }

    /// <summary>Scope mới không dọn dữ liệu, để mô phỏng hai request chạy song song trên cùng database.</summary>
    internal AsyncServiceScope CreateScope() => _serviceProvider!.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PromotionDatabaseCollection : ICollectionFixture<PromotionDatabaseFixture>
{
    public const string Name = "promotion-database";
}

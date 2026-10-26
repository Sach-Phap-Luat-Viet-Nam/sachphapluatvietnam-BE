using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory;
using SachLuat.Modules.Inventory.Infrastructure.Persistence;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.IntegrationTests.Inventory;

/// <summary>
/// Chạy migration Inventory lên CSDL thật trỏ bởi <see cref="ConnectionStringVariable"/>; thiếu biến thì skip.
/// Fixture không có module Catalog và Auth nên SKU và tên người dùng lấy từ bản giả ở cuối file.
/// </summary>
public sealed class InventoryDatabaseFixture : IAsyncLifetime
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
        services.AddLogging();
        new InventoryModule().AddServices(services, configuration);
        services.AddSingleton<ICatalogSkuReader, FakeSkuReader>();
        services.AddSingleton<IUserNameReader, FakeUserNames>();
        services.AddSingleton<ICurrentUser, AnonymousUser>();
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
    }

    /// <summary>Xóa sạch dữ liệu kho để mỗi test bắt đầu từ trạng thái trống.</summary>
    internal async Task<AsyncServiceScope> CreateCleanScopeAsync()
    {
        Assert.SkipWhen(
            _serviceProvider is null,
            $"Set {ConnectionStringVariable} to run database tests (see docs/setup.md).");
        var scope = _serviceProvider!.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        await dbContext.Transactions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.PrintBatches.ExecuteDeleteAsync(cancellationToken);
        await dbContext.StockItems.ExecuteDeleteAsync(cancellationToken);
        await dbContext.GoodsReceipts.ExecuteDeleteAsync(cancellationToken);
        return scope;
    }

    /// <summary>Scope mới không dọn dữ liệu, để đọc lại bằng DbContext sạch (không dùng cache của scope đã ghi).</summary>
    internal AsyncServiceScope CreateScopeForTests() => _serviceProvider!.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    /// <summary>Catalog giả: SKU 1 (Bộ luật Dân sự, bìa mềm), SKU 2 (Luật Đất đai, bìa cứng).</summary>
    private sealed class FakeSkuReader : ICatalogSkuReader
    {
        private static readonly IReadOnlyList<SkuInfo> Skus =
        [
            Sku(1, "PL-001", "Bộ luật Dân sự 2026", "paperback"),
            Sku(2, "PL-002", "Luật Đất đai 2026", "hardcover"),
        ];

        public Task<IReadOnlyDictionary<long, SkuInfo>> GetSkusAsync(
            IReadOnlyCollection<long> skuIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, SkuInfo>>(
                Skus.Where(sku => skuIds.Contains(sku.SkuId)).ToDictionary(sku => sku.SkuId));

        public Task<IReadOnlyList<SkuInfo>> SearchSkusAsync(string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SkuInfo>>(Skus
                .Where(sku => sku.BookTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || sku.SkuCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList());

        private static SkuInfo Sku(long id, string code, string title, string coverType) => new(
            id, code, id, title, $"sach-{id}", id, "Tái bản lần 1", "9780306406157", coverType,
            100_000m, 90_000m, 90_000m, null, null, true, [], true);
    }

    /// <summary>Test gọi service trực tiếp nên không có người dùng của request.</summary>
    private sealed class AnonymousUser : ICurrentUser
    {
        public long? UserId => null;

        public long? ImpersonatorUserId => null;

        public bool IsInRole(string role) => false;
    }

    private sealed class FakeUserNames : IUserNameReader
    {
        public Task<IReadOnlyDictionary<long, string>> GetNamesAsync(
            IReadOnlyCollection<long> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, string>>(
                userIds.Distinct().ToDictionary(id => id, id => $"Nhân viên {id}"));
    }
}

[CollectionDefinition(Name)]
public sealed class InventoryDatabaseCollection : ICollectionFixture<InventoryDatabaseFixture>
{
    public const string Name = "inventory-database";
}

using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Shipping;
using SachLuat.Modules.Shipping.Application;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.Modules.Shipping.Infrastructure.Persistence;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Shipping;

/// <summary>Chạy migration Shipping lên CSDL thật trỏ bởi <c>SACHLUAT_TEST_DB</c>; thiếu biến thì skip.</summary>
public sealed class ShippingDatabaseFixture : IAsyncLifetime
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
        new ShippingModule().AddServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShippingDbContext>().Database.MigrateAsync();
    }

    /// <summary>Xóa hết vùng và cấu hình đã lưu để mỗi test bắt đầu từ giá trị mặc định.</summary>
    internal async Task<AsyncServiceScope> CreateCleanScopeAsync()
    {
        Assert.SkipWhen(
            _serviceProvider is null,
            $"Set {ConnectionStringVariable} to run database tests (see docs/setup.md).");
        var scope = _serviceProvider!.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
        await dbContext.Zones.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await dbContext.Settings.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
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

[CollectionDefinition(Name)]
public sealed class ShippingDatabaseCollection : ICollectionFixture<ShippingDatabaseFixture>
{
    public const string Name = "shipping-database";
}

[Collection(ShippingDatabaseCollection.Name)]
public sealed class ShippingPersistenceTests(ShippingDatabaseFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static readonly ShippingItem[] OneBook = [new ShippingItem(500, 1)];

    [Fact]
    public async Task NoZonesAndNothingSaved_UsesConfiguredDefaultsAsFlatFee()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();

        var below = await Quote(scope).QuoteAsync("Hà Nội", OneBook, 100_000m, CancellationToken);
        var above = await Quote(scope).QuoteAsync(null, OneBook, 300_000m, CancellationToken);

        below.Fee.Should().Be(30_000m);
        below.ZoneName.Should().BeNull();
        above.Fee.Should().Be(0m);
        (await Admin(scope).GetSettingsAsync(CancellationToken)).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Zone_QuotesByProvinceWeightAndEta_IgnoringCaseDiacriticsAndPrefix()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateZoneAsync(Zone("Nội thành", ["Hà Nội", "Hồ Chí Minh"]), CancellationToken);

        var quote = await Quote(scope).QuoteAsync(
            "THÀNH PHỐ HÀ NỘI", [new ShippingItem(900, 2)], 100_000m, CancellationToken);

        // 1.800 g + 100 g bao bì = 1.900 g → 20.000 + 2 bước × 5.000.
        quote.Fee.Should().Be(30_000m);
        quote.ZoneName.Should().Be("Nội thành");
        (quote.EtaMinDays, quote.EtaMaxDays).Should().Be((1, 2));
    }

    [Fact]
    public async Task Zone_ProvinceOutsideEveryZone_IsNotSupported()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateZoneAsync(Zone("Nội thành", ["Hà Nội"]), CancellationToken);

        var act = () => Quote(scope).QuoteAsync("Cà Mau", OneBook, 100_000m, CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("ADDRESS_NOT_SUPPORTED");
    }

    [Fact]
    public async Task InactiveZone_DoesNotServeItsProvinces()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateZoneAsync(Zone("Đang chạy", ["Hà Nội"]), CancellationToken);
        await Admin(scope).CreateZoneAsync(Zone("Đã tắt", ["Đà Nẵng"]) with { IsActive = false }, CancellationToken);

        var act = () => Quote(scope).QuoteAsync("Đà Nẵng", OneBook, 100_000m, CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("ADDRESS_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Province_CannotBelongToTwoZones()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateZoneAsync(Zone("Vùng A", ["Hà Nội"]), CancellationToken);

        var act = () => Admin(scope).CreateZoneAsync(Zone("Vùng B", ["TP. Hà Nội", "Huế"]), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("SHIPPING_PROVINCE_DUPLICATE");
    }

    [Fact]
    public async Task UpdateZone_MovesProvincesWithoutConflictingWithItself()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var zone = await Admin(scope).CreateZoneAsync(Zone("Vùng A", ["Hà Nội", "Huế"]), CancellationToken);

        var updated = await Admin(scope).UpdateZoneAsync(
            zone.Id, Zone("Vùng A (sửa)", ["Hà Nội", "Đà Nẵng"]), CancellationToken);

        updated.Provinces.Should().BeEquivalentTo(["Hà Nội", "Đà Nẵng"]);
        (await Admin(scope).ListZonesAsync(CancellationToken)).Single().Name.Should().Be("Vùng A (sửa)");
    }

    [Fact]
    public async Task DeleteZone_FreesItsProvinces()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var zone = await Admin(scope).CreateZoneAsync(Zone("Vùng A", ["Hà Nội"]), CancellationToken);

        await Admin(scope).DeleteZoneAsync(zone.Id, CancellationToken);
        await Admin(scope).CreateZoneAsync(Zone("Vùng B", ["Hà Nội"]), CancellationToken);

        (await Admin(scope).ListZonesAsync(CancellationToken)).Should().ContainSingle();
    }

    [Fact]
    public async Task Settings_SavedValuesReplaceDefaults()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();

        await Admin(scope).UpdateSettingsAsync(new ShippingSettingsRequest(500_000m, 200, 350, 25_000m), CancellationToken);
        await Admin(scope).UpdateSettingsAsync(new ShippingSettingsRequest(null, 200, 350, 25_000m), CancellationToken);

        var saved = await Admin(scope).GetSettingsAsync(CancellationToken);
        saved.FreeShippingThreshold.Should().BeNull();
        saved.IsDefault.Should().BeFalse();
        var quote = await Quote(scope).QuoteAsync(null, OneBook, 10_000_000m, CancellationToken);
        quote.Fee.Should().Be(25_000m); // tắt miễn phí theo ngưỡng
    }

    private static ShippingQuoteService Quote(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ShippingQuoteService>();

    private static ShippingAdminService Admin(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ShippingAdminService>();

    private static UpsertZoneRequest Zone(string name, string[] provinces) => new(
        name, IsActive: true, SortOrder: 1, provinces, BaseWeightGram: 1000, BaseFee: 20_000m,
        StepWeightGram: 500, StepFee: 5_000m, EtaMinDays: 1, EtaMaxDays: 2);
}

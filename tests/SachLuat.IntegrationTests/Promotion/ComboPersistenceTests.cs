using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Promotion.Application;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Promotion;

/// <summary>
/// Combo trên CSDL thật. Fixture không có module Catalog nên SKU lấy từ <see cref="FakeSkuReader"/>:
/// SKU 1, 2, 3 giá 50.000đ đang bán, SKU 4 ngừng bán.
/// </summary>
[Collection(PromotionDatabaseCollection.Name)]
public sealed class ComboPersistenceTests(PromotionDatabaseFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private DateTimeOffset Now => fixture.Clock.GetUtcNow();

    [Fact]
    public async Task Create_PersistsTheBooksAndShowsCatalogDetails()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Admin(scope).CreateAsync(Request(), CancellationToken);

        await using var otherScope = fixture.CreateScope();
        var loaded = await Admin(otherScope).GetAsync(created.Id, CancellationToken);

        loaded.Slug.Should().Be("combo-ab");
        loaded.ComboPrice.Should().Be(70_000m);
        loaded.RegularTotal.Should().Be(100_000m);
        loaded.Status.Should().Be(PromotionStatus.Running);
        loaded.Items.Select(item => (item.SkuId, item.Quantity, item.SkuCode)).Should()
            .Equal((1L, 1, "SKU-1"), (2L, 1, "SKU-2"));
    }

    [Fact]
    public async Task Create_DuplicateSlug_IsConflict()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);

        var duplicate = () => Admin(scope).CreateAsync(
            Request() with { Items = [new ComboItemRequest(2, 1), new ComboItemRequest(3, 1)] }, CancellationToken);

        (await duplicate.Should().ThrowAsync<BusinessException>())
            .Which.Code.Should().Be(PromotionErrors.ComboSlugDuplicateCode);
    }

    [Fact]
    public async Task Create_SlugOfADeletedCombo_CanBeUsedAgain()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var first = await Admin(scope).CreateAsync(Request(), CancellationToken);
        await Admin(scope).DeleteAsync(first.Id, CancellationToken);

        var second = await Admin(scope).CreateAsync(Request(), CancellationToken);

        second.Id.Should().NotBe(first.Id);
        (await Admin(scope).ListAsync(new AdminCombosQuery(), CancellationToken)).Items
            .Should().ContainSingle().Which.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task Create_UnknownSku_IsRejected()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();

        var act = () => Admin(scope).CreateAsync(
            Request() with { Items = [new ComboItemRequest(1, 1), new ComboItemRequest(99, 1)] }, CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("REFERENCE_NOT_FOUND");
    }

    [Theory]
    [InlineData(100_000, false)] // bằng tổng giá bán thường: không phải combo
    [InlineData(120_000, false)]
    [InlineData(49_999, false)] // giảm hơn 50% so với giá bán thường: vượt trần Promotion:MaxDiscountPercent
    [InlineData(50_000, true)]
    [InlineData(70_000, true)]
    public async Task Create_PriceMustBeBelowRegularTotalAndWithinTheDiscountCap(int price, bool allowed)
    {
        await using var scope = await fixture.CreateCleanScopeAsync();

        var act = () => Admin(scope).CreateAsync(Request(price: price), CancellationToken);

        if (allowed)
        {
            (await act()).ComboPrice.Should().Be(price);
        }
        else
        {
            (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("VALIDATION_ERROR");
        }
    }

    [Fact]
    public async Task Update_ReplacesTheBooksAndQuantities()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Admin(scope).CreateAsync(Request(), CancellationToken);

        await Admin(scope).UpdateAsync(
            created.Id,
            Request(price: 120_000) with { Items = [new ComboItemRequest(1, 2), new ComboItemRequest(3, 1)] },
            CancellationToken);

        await using var otherScope = fixture.CreateScope();
        var loaded = await Admin(otherScope).GetAsync(created.Id, CancellationToken);
        loaded.ComboPrice.Should().Be(120_000m);
        loaded.Items.Select(item => (item.SkuId, item.Quantity)).Should().Equal((1L, 2), (3L, 1));
    }

    [Fact]
    public async Task Evaluate_AppliesTheRunningComboAndIgnoresTheOthers()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var running = await admin.CreateAsync(Request("combo-chay"), CancellationToken);
        await admin.CreateAsync(Request("combo-tat") with { IsActive = false }, CancellationToken);
        await admin.CreateAsync(Request("combo-chua-toi") with { StartsAt = Now.AddDays(1) }, CancellationToken);
        await admin.CreateAsync(
            Request("combo-het-han") with { StartsAt = Now.AddDays(-5), EndsAt = Now.AddDays(-1) }, CancellationToken);
        var deleted = await admin.CreateAsync(Request("combo-da-xoa"), CancellationToken);
        await admin.DeleteAsync(deleted.Id, CancellationToken);

        var result = await Service(scope).EvaluateAsync(Cart((1, 50_000m, 1), (2, 50_000m, 1)), CancellationToken);

        result.OrderDiscount.Should().Be(30_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 15_000m, [2] = 15_000m });
        result.Applied.Should().ContainSingle().Which.Should().Match<AppliedPromotion>(applied =>
            applied.PromotionId == running.Id && applied.Kind == PromotionKind.Combo && applied.Amount == 30_000m);
    }

    [Fact]
    public async Task Evaluate_CartWithoutAFullSet_AppliesNothing()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);

        var onlyOne = await Service(scope).EvaluateAsync(Cart((1, 50_000m, 1)), CancellationToken);
        var unrelated = await Service(scope).EvaluateAsync(Cart((3, 50_000m, 2)), CancellationToken);

        onlyOne.Applied.Should().BeEmpty();
        unrelated.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_ComboStacksWithAnOrderDiscountProgram_WithinTheTotalDiscountCap()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);
        await PromotionAdmin(scope).CreateAsync(
            ProgramRequest("Sale 40%", PromotionKind.OrderDiscount, 40m), CancellationToken);

        var result = await Service(scope).EvaluateAsync(Cart((1, 50_000m, 1), (2, 50_000m, 1)), CancellationToken);

        // Combo giảm 30.000đ (còn 70.000đ). 40% × 70.000đ = 28.000đ nhưng trần tổng giảm
        // Promotion:MaxTotalDiscountPercent = 50% của giá bán thường 100.000đ chỉ còn chỗ cho 20.000đ: tổng đúng 50.000đ.
        result.Applied.Select(applied => (applied.Kind, applied.Amount)).Should().Equal(
            (PromotionKind.Combo, 30_000m), (PromotionKind.OrderDiscount, 20_000m));
        result.OrderDiscount.Should().Be(50_000m);
    }

    [Fact]
    public async Task Redeem_SkipsTheCombo_ButStillRecordsTheShippingProgram()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);
        var shipping = await PromotionAdmin(scope).CreateAsync(
            ProgramRequest("Freeship", PromotionKind.FreeShipping, 100m), CancellationToken);
        var evaluation = await Service(scope).EvaluateAsync(
            Cart((1, 50_000m, 1), (2, 50_000m, 1)), CancellationToken);

        await Service(scope).RedeemAsync(
            "SLVN2611030001", evaluation, new PromotionCustomer(null, "0900000001"), CancellationToken);
        await Service(scope).ReleaseAsync("SLVN2611030001", CancellationToken);

        evaluation.Applied.Select(applied => applied.Kind).Should()
            .Equal(PromotionKind.Combo, PromotionKind.FreeShipping);
        var dbContext = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var redemption = await dbContext.Redemptions.SingleAsync(CancellationToken);
        redemption.PromotionId.Should().Be(shipping.Id);
        redemption.Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public async Task Redeem_ComboOnly_WritesNothing()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);
        var evaluation = await Service(scope).EvaluateAsync(
            Cart((1, 50_000m, 1), (2, 50_000m, 1)), CancellationToken);

        await Service(scope).RedeemAsync(
            "SLVN2611030001", evaluation, new PromotionCustomer(null, "0900000001"), CancellationToken);

        evaluation.Applied.Should().ContainSingle().Which.Kind.Should().Be(PromotionKind.Combo);
        var dbContext = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        (await dbContext.Redemptions.CountAsync(CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Storefront_ListsRunningSellableCombos_AndFiltersBySku()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        await admin.CreateAsync(Request("combo-ab"), CancellationToken);
        await admin.CreateAsync(
            Request("combo-bc") with { Items = [new ComboItemRequest(2, 1), new ComboItemRequest(3, 1)] },
            CancellationToken);
        await admin.CreateAsync(
            Request("combo-ngung-ban") with { Items = [new ComboItemRequest(3, 1), new ComboItemRequest(4, 1)] },
            CancellationToken);
        await admin.CreateAsync(Request("combo-tat") with { IsActive = false }, CancellationToken);

        var all = await Storefront(scope).ListAsync(null, CancellationToken);
        var withSku1 = await Storefront(scope).ListAsync(1, CancellationToken);
        var withSku2 = await Storefront(scope).ListAsync(2, CancellationToken);

        all.Select(combo => combo.Slug).Should().Equal("combo-bc", "combo-ab"); // mới tạo trước, ngừng bán và tắt bị ẩn
        withSku1.Select(combo => combo.Slug).Should().Equal("combo-ab");
        withSku2.Select(combo => combo.Slug).Should().Equal("combo-bc", "combo-ab");
    }

    [Fact]
    public async Task Storefront_Detail_ShowsCurrentPricesAndSavings()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request(), CancellationToken);

        // SKU 1 đang chạy lịch giá 40.000đ nên giá lẻ hiện hành của bộ là 90.000đ.
        var reader = new FakeSkuReader(Sku(1, sale: 40_000m), Sku(2));
        var combo = await Storefront(scope, reader).GetAsync("combo-ab", CancellationToken);

        combo.RetailPrice.Should().Be(90_000m);
        combo.ListPrice.Should().Be(100_000m); // giá bìa không đổi khi có lịch giá
        combo.Savings.Should().Be(20_000m);
        combo.IsSellable.Should().BeTrue();
        combo.Items.Select(item => (item.SkuId, item.UnitPrice, item.BookTitle)).Should()
            .Equal((1L, 40_000m, "Sách 1"), (2L, 50_000m, "Sách 2"));
    }

    [Fact]
    public async Task Storefront_Detail_StoppedBookKeepsThePageButTurnsOffBuying_AndHiddenCombosAreNotFound()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        await admin.CreateAsync(
            Request("combo-ngung-ban") with { Items = [new ComboItemRequest(3, 1), new ComboItemRequest(4, 1)] },
            CancellationToken);
        await admin.CreateAsync(Request("combo-tat") with { IsActive = false }, CancellationToken);

        var stopped = await Storefront(scope).GetAsync("combo-ngung-ban", CancellationToken);
        var switchedOff = () => Storefront(scope).GetAsync("combo-tat", CancellationToken);
        var unknown = () => Storefront(scope).GetAsync("khong-co", CancellationToken);

        stopped.IsSellable.Should().BeFalse();
        (await switchedOff.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("NOT_FOUND");
        (await unknown.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("NOT_FOUND");
    }

    private static PromotionService Service(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<PromotionService>();

    private static PromotionAdminService PromotionAdmin(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<PromotionAdminService>();

    private static PromotionRequest Cart(params (long SkuId, decimal UnitPrice, int Quantity)[] lines) => new(
        [.. lines.Select(line => new PromotionLine(
            line.SkuId, line.SkuId, [], line.UnitPrice, line.Quantity, IsOnSale: false))],
        ShippingFee: 30_000m,
        new PromotionCustomer(null, "0900000099"),
        CouponCode: null);

    private static SkuInfo Sku(long id, decimal regular = 50_000m, decimal? sale = null, bool sellable = true) => new(
        id,
        $"SKU-{id}",
        BookId: id,
        $"Sách {id}",
        $"sach-{id}",
        EditionId: id,
        "Bản 2026",
        "9780000000000",
        "paperback",
        ListPrice: regular,
        SalePrice: sale ?? regular,
        RegularSalePrice: regular,
        WeightGram: 300,
        CoverImageUrl: null,
        IsLatestEdition: true,
        CategoryIds: [],
        IsSellable: sellable);

    private static FakeSkuReader DefaultSkus() => new(Sku(1), Sku(2), Sku(3), Sku(4, sellable: false));

    private ComboAdminService Admin(AsyncServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<PromotionDbContext>(),
        DefaultSkus(),
        Options.Create(new PromotionOptions()),
        fixture.Clock,
        scope.ServiceProvider.GetRequiredService<ILogger<ComboAdminService>>());

    private ComboStorefrontService Storefront(AsyncServiceScope scope, FakeSkuReader? reader = null) => new(
        scope.ServiceProvider.GetRequiredService<PromotionDbContext>(),
        reader ?? DefaultSkus(),
        fixture.Clock);

    private UpsertComboRequest Request(string slug = "combo-ab", int price = 70_000) => new(
        Name: $"Combo {slug}",
        slug,
        Description: null,
        ImageUrl: null,
        price,
        Items: [new ComboItemRequest(1, 1), new ComboItemRequest(2, 1)],
        StartsAt: Now.AddDays(-1),
        EndsAt: null,
        IsActive: true);

    private UpsertPromotionRequest ProgramRequest(string name, PromotionKind kind, decimal percent) => new(
        name,
        name,
        Description: null,
        kind,
        PromotionMode.Automatic,
        DiscountType.Percent,
        DiscountValue: percent,
        MaxDiscountAmount: null,
        MinOrderAmount: 0m,
        PromotionScopeType.All,
        TargetIds: null,
        ExcludeOnSaleItems: false,
        PromotionAudience.Everyone,
        UsageLimit: null,
        PerCustomerLimit: null,
        StartsAt: Now.AddDays(-1),
        EndsAt: null,
        IsActive: true,
        Priority: 0,
        IsPublic: true);

    /// <summary>Catalog giả: chỉ trả các SKU đã khai báo.</summary>
    private sealed class FakeSkuReader(params SkuInfo[] skus) : ICatalogSkuReader
    {
        public Task<IReadOnlyDictionary<long, SkuInfo>> GetSkusAsync(
            IReadOnlyCollection<long> skuIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, SkuInfo>>(
                skus.Where(sku => skuIds.Contains(sku.SkuId)).ToDictionary(sku => sku.SkuId));

        public Task<IReadOnlyList<SkuInfo>> SearchSkusAsync(string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SkuInfo>>(
                skus.Where(sku => sku.BookTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList());
    }
}

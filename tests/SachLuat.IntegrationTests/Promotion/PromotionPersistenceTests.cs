using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Promotion.Application;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Promotion;

[Collection(PromotionDatabaseCollection.Name)]
public sealed class PromotionPersistenceTests(PromotionDatabaseFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private DateTimeOffset Now => fixture.Clock.GetUtcNow();

    [Fact]
    public async Task Evaluate_TwentyPercentAutomatic_DiscountsTheWholeOrder()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request("Sale 20%"), CancellationToken);

        var result = await Service(scope).EvaluateAsync(Cart(150_000m, 2), CancellationToken);

        result.OrderDiscount.Should().Be(60_000m);
        result.LineDiscounts[1].Should().Be(60_000m);
        result.Applied.Should().ContainSingle();
    }

    [Fact]
    public async Task Evaluate_IgnoresScheduledEndedSwitchedOffDeletedAndExhaustedCampaigns()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        await admin.CreateAsync(Request("Chưa tới lịch") with { StartsAt = Now.AddDays(1) }, CancellationToken);
        await admin.CreateAsync(
            Request("Đã hết lịch") with { StartsAt = Now.AddDays(-5), EndsAt = Now.AddDays(-1) }, CancellationToken);
        await admin.CreateAsync(Request("Đã tắt") with { IsActive = false }, CancellationToken);
        await admin.CreateAsync(Request("Hết lượt") with { UsageLimit = 1 }, CancellationToken);
        var deleted = await admin.CreateAsync(Request("Đã xóa"), CancellationToken);
        await admin.DeleteAsync(deleted.Id, CancellationToken);

        // Chương trình "Hết lượt" còn lượt cho tới khi có người dùng; tắt bằng cách dùng hết lượt duy nhất.
        var exhausted = (await admin.ListAsync(new AdminPromotionsQuery { Q = "Hết lượt" }, CancellationToken)).Items[0];
        await Service(scope).RedeemAsync(
            "SLVN2611030001", await EvaluateAsync(scope, Cart(150_000m, 1)), Customer("0900000001"), CancellationToken);
        (await admin.GetAsync(exhausted.Id, CancellationToken)).Status.Should().Be(PromotionStatus.Exhausted);

        var result = await Service(scope).EvaluateAsync(Cart(150_000m, 1), CancellationToken);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_CouponIsCaseInsensitive_AndUnknownCodeIsInvalid()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var promotion = await admin.CreateAsync(
            Request("Mã MUATHI") with { Mode = PromotionMode.Coupon, DiscountValue = 10m }, CancellationToken);
        await admin.CreateCouponAsync(promotion.Id, new CreateCouponRequest("muathi2026", null), CancellationToken);

        var applied = await Service(scope).EvaluateAsync(Cart(100_000m, 1, "MuaThi2026"), CancellationToken);
        var unknown = () => Service(scope).EvaluateAsync(Cart(100_000m, 1, "KHONGCO"), CancellationToken);

        applied.CouponApplied.Should().BeTrue();
        applied.OrderDiscount.Should().Be(10_000m);
        (await unknown.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("COUPON_INVALID");
    }

    [Fact]
    public async Task Redeem_IsIdempotentPerOrder_AndCountsOneUse()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var promotion = await Admin(scope).CreateAsync(Request("Sale 20%"), CancellationToken);
        var evaluation = await EvaluateAsync(scope, Cart(150_000m, 1));

        await Service(scope).RedeemAsync("SLVN2611030001", evaluation, Customer("0900000001"), CancellationToken);
        await Service(scope).RedeemAsync("SLVN2611030001", evaluation, Customer("0900000001"), CancellationToken);

        (await Admin(scope).GetAsync(promotion.Id, CancellationToken)).UsedCount.Should().Be(1);
        (await Admin(scope).GetStatsAsync(promotion.Id, CancellationToken)).Should().Match<PromotionStatsResponse>(
            stats => stats.TotalOrderDiscount == 30_000m && stats.UniqueCustomers == 1);
    }

    [Fact]
    public async Task Redeem_LastRemainingUse_GoesToExactlyOneOfTwoConcurrentOrders()
    {
        await using var seedScope = await fixture.CreateCleanScopeAsync();
        var promotion = await Admin(seedScope).CreateAsync(
            Request("Chỉ 1 lượt") with { UsageLimit = 1 }, CancellationToken);
        var evaluation = await EvaluateAsync(seedScope, Cart(150_000m, 1));

        async Task<Exception?> TryRedeemAsync(string orderCode, string phone)
        {
            await using var scope = fixture.CreateScope();
            try
            {
                await Service(scope).RedeemAsync(orderCode, evaluation, Customer(phone), CancellationToken);
                return null;
            }
            catch (BusinessException exception)
            {
                return exception;
            }
        }

        var outcomes = await Task.WhenAll(
            TryRedeemAsync("SLVN2611030001", "0900000001"), TryRedeemAsync("SLVN2611030002", "0900000002"));

        outcomes.Count(outcome => outcome is null).Should().Be(1);
        outcomes.OfType<BusinessException>().Single().Code.Should().Be(PromotionErrors.PromotionUnavailableCode);
        (await Admin(seedScope).GetAsync(promotion.Id, CancellationToken)).UsedCount.Should().Be(1);
    }

    [Fact]
    public async Task Redeem_ExhaustedCoupon_ThrowsCouponExpiredAndWritesNothing()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var promotion = await admin.CreateAsync(
            Request("Mã một lần") with { Mode = PromotionMode.Coupon }, CancellationToken);
        await admin.CreateCouponAsync(promotion.Id, new CreateCouponRequest("ONCE2026", 1), CancellationToken);
        var evaluation = await EvaluateAsync(scope, Cart(150_000m, 1, "ONCE2026"));

        await Service(scope).RedeemAsync("SLVN2611030001", evaluation, Customer("0900000001"), CancellationToken);
        var second = () => Service(scope).RedeemAsync(
            "SLVN2611030002", evaluation, Customer("0900000002"), CancellationToken);

        (await second.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("COUPON_EXPIRED");
        var dbContext = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        (await dbContext.Redemptions.CountAsync(CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Redeem_PerCustomerLimit_BlocksTheSamePhoneButNotAnother()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        await Admin(scope).CreateAsync(Request("Mỗi khách 1 lần") with { PerCustomerLimit = 1 }, CancellationToken);
        var evaluation = await EvaluateAsync(scope, Cart(150_000m, 1));

        await Service(scope).RedeemAsync("SLVN2611030001", evaluation, Customer("0901234567"), CancellationToken);
        var samePhoneDifferentFormat = () => Service(scope).RedeemAsync(
            "SLVN2611030002", evaluation, Customer("+84901234567"), CancellationToken);
        await Service(scope).RedeemAsync("SLVN2611030003", evaluation, Customer("0907654321"), CancellationToken);

        (await samePhoneDifferentFormat.Should().ThrowAsync<BusinessException>())
            .Which.Code.Should().Be(PromotionErrors.PromotionUnavailableCode);
    }

    [Fact]
    public async Task Release_ReturnsTheUseOnce_AndIsIdempotent()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var promotion = await Admin(scope).CreateAsync(
            Request("Chỉ 1 lượt") with { UsageLimit = 1 }, CancellationToken);
        await Service(scope).RedeemAsync(
            "SLVN2611030001", await EvaluateAsync(scope, Cart(150_000m, 1)), Customer("0900000001"), CancellationToken);

        await Service(scope).ReleaseAsync("SLVN2611030001", CancellationToken);
        await Service(scope).ReleaseAsync("SLVN2611030001", CancellationToken);

        var after = await Admin(scope).GetAsync(promotion.Id, CancellationToken);
        after.UsedCount.Should().Be(0);
        after.Status.Should().Be(PromotionStatus.Running);
        (await Admin(scope).GetStatsAsync(promotion.Id, CancellationToken)).ReleasedCount.Should().Be(1);

        // Lượt đã trả dùng lại được cho đơn khác.
        await Service(scope).RedeemAsync(
            "SLVN2611030002", await EvaluateAsync(scope, Cart(150_000m, 1)), Customer("0900000002"), CancellationToken);
    }

    [Fact]
    public async Task Release_AlsoReturnsTheCouponUse()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var promotion = await admin.CreateAsync(
            Request("Mã một lần") with { Mode = PromotionMode.Coupon }, CancellationToken);
        var coupon = await admin.CreateCouponAsync(promotion.Id, new CreateCouponRequest("ONCE2026", 1), CancellationToken);
        await Service(scope).RedeemAsync(
            "SLVN2611030001",
            await EvaluateAsync(scope, Cart(150_000m, 1, "ONCE2026")),
            Customer("0900000001"),
            CancellationToken);

        await Service(scope).ReleaseAsync("SLVN2611030001", CancellationToken);

        var coupons = await admin.ListCouponsAsync(promotion.Id, new PagedQuery(), CancellationToken);
        coupons.Items.Single(item => item.Id == coupon.Id).UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task AdminList_ReturnsTargetsAndCouponCounts()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var promotion = await admin.CreateAsync(
            Request("Theo sách") with
            {
                Mode = PromotionMode.Coupon,
                ScopeType = PromotionScopeType.Books,
                TargetIds = [7, 8],
            },
            CancellationToken);
        await admin.GenerateCouponsAsync(promotion.Id, new GenerateCouponsRequest(5, "GT-", null), CancellationToken);

        var page = await admin.ListAsync(new AdminPromotionsQuery(), CancellationToken);

        var item = page.Items.Single();
        item.TargetIds.Should().Equal(7L, 8L);
        item.CouponCount.Should().Be(5);
    }

    [Fact]
    public async Task GenerateCoupons_CreatesDistinctOneTimeCodes_OnlyForCouponPrograms()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var automatic = await admin.CreateAsync(Request("Tự áp"), CancellationToken);
        var couponProgram = await admin.CreateAsync(Request("Dùng mã") with { Mode = PromotionMode.Coupon }, CancellationToken);

        var notCoupon = () => admin.GenerateCouponsAsync(
            automatic.Id, new GenerateCouponsRequest(3, null, null), CancellationToken);
        var generated = await admin.GenerateCouponsAsync(
            couponProgram.Id, new GenerateCouponsRequest(50, "GT-", null), CancellationToken);

        (await notCoupon.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("PROMOTION_NOT_COUPON");
        generated.Should().HaveCount(50);
        generated.Select(coupon => coupon.Code).Distinct().Should().HaveCount(50);
        generated.Should().OnlyContain(coupon => coupon.UsageLimit == 1 && coupon.Code.StartsWith("GT-"));
    }

    [Fact]
    public async Task CreateCoupon_DuplicateCode_IsConflict()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var promotion = await admin.CreateAsync(Request("Dùng mã") with { Mode = PromotionMode.Coupon }, CancellationToken);
        await admin.CreateCouponAsync(promotion.Id, new CreateCouponRequest("DUP2026", null), CancellationToken);

        var duplicate = () => admin.CreateCouponAsync(promotion.Id, new CreateCouponRequest("dup2026", null), CancellationToken);

        (await duplicate.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be("COUPON_CODE_DUPLICATE");
    }

    [Fact]
    public async Task StorefrontActive_ListsRunningPublicPromotionsOnly()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        await admin.CreateAsync(Request("Công khai") with { Priority = 1 }, CancellationToken);
        await admin.CreateAsync(Request("Mã riêng") with { IsPublic = false, Priority = 9 }, CancellationToken);
        await admin.CreateAsync(Request("Đã tắt") with { IsActive = false }, CancellationToken);

        var active = await scope.ServiceProvider.GetRequiredService<PromotionStorefrontService>()
            .GetActiveAsync(CancellationToken);

        active.Should().ContainSingle().Which.Title.Should().Be("Giảm ngay 20% trên tổng hóa đơn");
    }

    [Fact]
    public async Task Update_ChangesTargetsAndKeepsUsage()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var admin = Admin(scope);
        var created = await admin.CreateAsync(
            Request("Theo sách") with { ScopeType = PromotionScopeType.Books, TargetIds = [1, 2, 3] },
            CancellationToken);
        await Service(scope).RedeemAsync(
            "SLVN2611030001",
            await EvaluateAsync(scope, Cart(150_000m, 1, bookId: 2)),
            Customer("0900000001"),
            CancellationToken);

        var updated = await admin.UpdateAsync(
            created.Id,
            Request("Theo sách (sửa)") with { ScopeType = PromotionScopeType.Books, TargetIds = [3, 4] },
            CancellationToken);

        updated.TargetIds.Should().Equal(3L, 4L);
        updated.UsedCount.Should().Be(1);
        updated.Name.Should().Be("Theo sách (sửa)");
    }

    private static PromotionService Service(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<PromotionService>();

    private static PromotionAdminService Admin(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<PromotionAdminService>();

    private static PromotionCustomer Customer(string phone) => new(UserId: null, phone);

    private static PromotionRequest Cart(decimal unitPrice, int quantity, string? couponCode = null, long bookId = 1) => new(
        [new PromotionLine(1, bookId, [], unitPrice, quantity, IsOnSale: false)],
        ShippingFee: 30_000m,
        Customer("0900000099"),
        couponCode);

    private static Task<PromotionEvaluation> EvaluateAsync(AsyncServiceScope scope, PromotionRequest request) =>
        Service(scope).EvaluateAsync(request, CancellationToken);

    private UpsertPromotionRequest Request(string name) => new(
        name,
        "Giảm ngay 20% trên tổng hóa đơn",
        Description: null,
        PromotionKind.OrderDiscount,
        PromotionMode.Automatic,
        DiscountType.Percent,
        DiscountValue: 20m,
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
}

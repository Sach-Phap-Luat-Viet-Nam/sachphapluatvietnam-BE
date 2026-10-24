using AwesomeAssertions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;
using static SachLuat.UnitTests.Modules.Promotion.PromotionTestData;

namespace SachLuat.UnitTests.Modules.Promotion;

public sealed class PromotionEvaluatorTests
{
    // 2 × 150.000 + 1 × 150.000 = 450.000đ
    private static readonly PromotionLine[] ThreeBooks = [Line(1, 150_000m, quantity: 2), Line(2, 150_000m)];

    [Fact]
    public void AutomaticPercent_DiscountsWholeOrderAndSplitsAcrossLines()
    {
        var result = Evaluate(Request(ThreeBooks), [Auto(Campaign(value: 20m))]);

        result.OrderDiscount.Should().Be(90_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 60_000m, [2] = 30_000m });
        result.Applied.Should().ContainSingle().Which.Should().Match<AppliedPromotion>(applied =>
            applied.PromotionId == 1 && applied.Kind == PromotionKind.OrderDiscount && applied.CouponCode == null
            && applied.Amount == 90_000m);
        result.CouponApplied.Should().BeFalse();
    }

    [Fact]
    public void Percent_RoundsDownToWholeDong()
    {
        var result = Evaluate(Request([Line(1, 100_001m)]), [Auto(Campaign(value: 20m))]);

        result.OrderDiscount.Should().Be(20_000m); // 20.000,2đ
    }

    [Fact]
    public void Percent_IsCappedByMaxDiscountAmount()
    {
        var result = Evaluate(Request([Line(1, 2_000_000m)]), [Auto(Campaign(value: 20m, maxDiscount: 200_000m))]);

        result.OrderDiscount.Should().Be(200_000m);
    }

    [Fact]
    public void FixedAmount_NeverExceedsEligibleSubtotal()
    {
        var result = Evaluate(
            Request([Line(1, 300_000m)]),
            [Auto(Campaign(discountType: DiscountType.FixedAmount, value: 500_000m))]);

        result.OrderDiscount.Should().Be(300_000m);
    }

    [Fact]
    public void Automatic_BelowMinOrder_IsSilentlySkipped()
    {
        var result = Evaluate(Request(ThreeBooks), [Auto(Campaign(minOrder: 500_000m))]);

        result.Applied.Should().BeEmpty();
        result.OrderDiscount.Should().Be(0m);
    }

    [Fact]
    public void Automatic_Exhausted_IsSilentlySkipped()
    {
        var result = Evaluate(Request(ThreeBooks), [Auto(Campaign(usageLimit: 5, usedCount: 5))]);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public void Scope_Books_OnlyDiscountsMatchingBooks()
    {
        var lines = new[] { Line(1, 100_000m, bookId: 7), Line(2, 100_000m, bookId: 8) };

        var result = Evaluate(
            Request(lines), [Auto(Campaign(scope: PromotionScopeType.Books, targets: [7], value: 50m))]);

        result.OrderDiscount.Should().Be(50_000m);
        result.LineDiscounts[1].Should().Be(50_000m);
        result.LineDiscounts.GetValueOrDefault(2).Should().Be(0m);
    }

    [Fact]
    public void Scope_Categories_MatchesAncestorCategoriesOfTheBook()
    {
        // Sách thuộc danh mục 5 có cha 2; chương trình đặt trên danh mục cha 2.
        var lines = new[] { Line(1, 100_000m, categoryIds: [5, 2]), Line(2, 100_000m, categoryIds: [9]) };

        var result = Evaluate(
            Request(lines), [Auto(Campaign(scope: PromotionScopeType.Categories, targets: [2], value: 10m))]);

        result.LineDiscounts[1].Should().Be(10_000m);
        result.OrderDiscount.Should().Be(10_000m);
    }

    [Fact]
    public void Scope_Skus_OnlyDiscountsListedSkus()
    {
        var result = Evaluate(
            Request([Line(1, 100_000m), Line(2, 100_000m)]),
            [Auto(Campaign(scope: PromotionScopeType.Skus, targets: [2], value: 10m))]);

        result.LineDiscounts[2].Should().Be(10_000m);
    }

    [Fact]
    public void ExcludeOnSaleItems_SkipsBooksAlreadyOnPriceSchedule()
    {
        var lines = new[] { Line(1, 100_000m, isOnSale: true), Line(2, 100_000m) };

        var result = Evaluate(Request(lines), [Auto(Campaign(value: 20m, excludeOnSale: true))]);

        result.OrderDiscount.Should().Be(20_000m);
        result.LineDiscounts.GetValueOrDefault(1).Should().Be(0m);
    }

    [Fact]
    public void MinOrder_IsCheckedAgainstWholeOrderNotOnlyEligibleLines()
    {
        var lines = new[] { Line(1, 100_000m, bookId: 7), Line(2, 400_000m, bookId: 8) };

        var result = Evaluate(
            Request(lines),
            [Auto(Campaign(scope: PromotionScopeType.Books, targets: [7], value: 10m, minOrder: 500_000m))]);

        result.OrderDiscount.Should().Be(10_000m);
    }

    [Fact]
    public void TwoAutomatic_PicksTheOneWithBiggerDiscount()
    {
        var result = Evaluate(
            Request(ThreeBooks),
            [Auto(Campaign(id: 1, value: 10m)), Auto(Campaign(id: 2, value: 20m)), Auto(Campaign(id: 3, value: 15m))]);

        result.Applied.Should().ContainSingle().Which.PromotionId.Should().Be(2);
        result.OrderDiscount.Should().Be(90_000m);
    }

    [Fact]
    public void Tie_PrefersCouponTheCustomerTyped()
    {
        var coupon = NewCoupon(promotionId: 2);

        var result = Evaluate(
            Request(ThreeBooks, couponCode: "MUATHI2026"),
            [Auto(Campaign(id: 1, value: 20m)), new PromotionCandidate(Campaign(id: 2, mode: PromotionMode.Coupon), coupon)]);

        result.Applied.Should().ContainSingle().Which.PromotionId.Should().Be(2);
        result.CouponApplied.Should().BeTrue();
        result.Applied[0].CouponCode.Should().Be("MUATHI2026");
        result.Applied[0].CouponId.Should().Be(10);
    }

    [Fact]
    public void Tie_WithoutCoupon_PrefersHigherPriorityThenOlderCampaign()
    {
        var byPriority = Evaluate(
            Request(ThreeBooks), [Auto(Campaign(id: 1, priority: 1)), Auto(Campaign(id: 2, priority: 5))]);
        var byAge = Evaluate(Request(ThreeBooks), [Auto(Campaign(id: 4)), Auto(Campaign(id: 3))]);

        byPriority.Applied.Single().PromotionId.Should().Be(2);
        byAge.Applied.Single().PromotionId.Should().Be(3);
    }

    [Fact]
    public void ValidCoupon_LosingToBetterAutomaticOffer_IsNotApplied()
    {
        var coupon = NewCoupon(promotionId: 2);

        var result = Evaluate(
            Request(ThreeBooks, couponCode: "MUATHI2026"),
            [
                Auto(Campaign(id: 1, value: 30m)),
                new PromotionCandidate(Campaign(id: 2, mode: PromotionMode.Coupon, value: 10m), coupon),
            ]);

        result.Applied.Should().ContainSingle().Which.PromotionId.Should().Be(1);
        result.CouponApplied.Should().BeFalse();
    }

    [Fact]
    public void OrderDiscountAndFreeShipping_AreAppliedTogether()
    {
        var result = Evaluate(
            Request(ThreeBooks, shippingFee: 30_000m),
            [
                Auto(Campaign(id: 1, value: 20m)),
                Auto(Campaign(id: 2, kind: PromotionKind.FreeShipping, value: 100m)),
            ]);

        result.OrderDiscount.Should().Be(90_000m);
        result.ShippingDiscount.Should().Be(30_000m);
        result.Applied.Select(applied => applied.Kind).Should()
            .BeEquivalentTo([PromotionKind.OrderDiscount, PromotionKind.FreeShipping]);
    }

    [Fact]
    public void FreeShipping_FixedAmount_IsCappedByShippingFee()
    {
        var result = Evaluate(
            Request(ThreeBooks, shippingFee: 30_000m),
            [Auto(Campaign(kind: PromotionKind.FreeShipping, discountType: DiscountType.FixedAmount, value: 50_000m))]);

        result.ShippingDiscount.Should().Be(30_000m);
    }

    [Fact]
    public void FreeShipping_WhenShippingAlreadyFree_AppliesNothing()
    {
        var result = Evaluate(
            Request(ThreeBooks, shippingFee: 0m),
            [Auto(Campaign(kind: PromotionKind.FreeShipping, value: 100m))]);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public void MembersOnly_Automatic_SkippedForGuest_AppliedForMember()
    {
        var campaign = Campaign(audience: PromotionAudience.MembersOnly);

        Evaluate(Request(ThreeBooks), [Auto(campaign)]).Applied.Should().BeEmpty();
        Evaluate(Request(ThreeBooks, userId: 5), [Auto(campaign)]).Applied.Should().ContainSingle();
    }

    [Fact]
    public void PerCustomerLimit_ReachedForAutomatic_IsSkipped()
    {
        var campaign = Campaign(perCustomerLimit: 1);

        var result = Evaluate(Request(ThreeBooks), [Auto(campaign)], new Dictionary<long, int> { [1] = 1 });

        result.Applied.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ended", "COUPON_EXPIRED")]
    [InlineData("not started", "COUPON_INVALID")]
    [InlineData("switched off", "COUPON_EXPIRED")]
    [InlineData("campaign exhausted", "COUPON_EXPIRED")]
    [InlineData("min order", "COUPON_MIN_ORDER")]
    [InlineData("members only", "COUPON_LOGIN_REQUIRED")]
    [InlineData("per customer", "COUPON_EXPIRED")]
    [InlineData("no eligible book", "COUPON_NOT_APPLICABLE")]
    public void Coupon_NotUsable_ThrowsExplanatoryError(string scenario, string expectedCode)
    {
        var mode = PromotionMode.Coupon;
        long? userId = null;
        var usage = new Dictionary<long, int>();
        var campaign = scenario switch
        {
            "ended" => Campaign(mode: mode, endsAt: Now.AddMinutes(-1)),
            "not started" => Campaign(mode: mode, startsAt: Now.AddHours(1)),
            "switched off" => Campaign(mode: mode, isActive: false),
            "campaign exhausted" => Campaign(mode: mode, usageLimit: 1, usedCount: 1),
            "min order" => Campaign(mode: mode, minOrder: 1_000_000m),
            "members only" => Campaign(mode: mode, audience: PromotionAudience.MembersOnly),
            "per customer" => Campaign(mode: mode, perCustomerLimit: 1),
            "no eligible book" => Campaign(mode: mode, scope: PromotionScopeType.Books, targets: [999]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        if (scenario == "per customer")
        {
            userId = 5;
            usage[campaign.Id] = 1;
        }

        var act = () => Evaluate(
            Request(ThreeBooks, userId: userId, couponCode: "MUATHI2026"),
            [new PromotionCandidate(campaign, NewCoupon(promotionId: campaign.Id))],
            usage);

        act.Should().Throw<BusinessException>(scenario).Which.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Coupon_ItselfExhaustedOrSwitchedOff_Throws()
    {
        var campaign = Campaign(mode: PromotionMode.Coupon);
        var used = NewCoupon(usageLimit: 1);
        used.IncrementUsage();

        var exhausted = () => Evaluate(Request(ThreeBooks), [new PromotionCandidate(campaign, used)]);

        exhausted.Should().Throw<BusinessException>().Which.Code.Should().Be("COUPON_EXPIRED");
    }

    [Theory]
    [InlineData(100, 3, new[] { 100, 100, 100 }, 100)]
    [InlineData(10, 3, new[] { 1, 1, 1 }, 10)]
    [InlineData(7, 2, new[] { 100, 100 }, 7)]
    public void Allocate_AlwaysSumsToTheDiscount(int discount, int lineCount, int[] totals, int expectedSum)
    {
        var lines = Enumerable.Range(1, lineCount).Select(index => ((long)index, (decimal)totals[index - 1])).ToList();

        var result = PromotionEvaluator.Allocate(lines, discount);

        result.Values.Sum().Should().Be(expectedSum);
        result.Values.Should().OnlyContain(amount => amount >= 0);
    }

    [Fact]
    public void Allocate_GivesTheRemainderToTheLargestLine()
    {
        var result = PromotionEvaluator.Allocate([(1L, 100m), (2L, 100m), (3L, 100m)], 100m);

        // 100 / 3 = 33,33 → mỗi dòng 33, dư 1đ vào dòng lớn nhất (hòa thì SKU nhỏ nhất).
        result.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 34m, [2] = 33m, [3] = 33m });
    }

    [Fact]
    public void Allocate_NoDiscount_ReturnsZeros()
    {
        PromotionEvaluator.Allocate([(1L, 100m)], 0m).Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 0m });
    }

    [Fact]
    public void None_HasNoDiscounts()
    {
        PromotionEvaluation.None.Applied.Should().BeEmpty();
        PromotionEvaluation.None.OrderDiscount.Should().Be(0m);
    }
}

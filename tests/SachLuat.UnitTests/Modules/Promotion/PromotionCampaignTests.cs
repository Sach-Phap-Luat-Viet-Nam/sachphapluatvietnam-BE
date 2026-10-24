using AwesomeAssertions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;
using static SachLuat.UnitTests.Modules.Promotion.PromotionTestData;

namespace SachLuat.UnitTests.Modules.Promotion;

public sealed class PromotionCampaignTests
{
    [Fact]
    public void StatusAt_FollowsTheSchedule()
    {
        var campaign = Campaign(startsAt: Now.AddDays(1), endsAt: Now.AddDays(10));

        campaign.StatusAt(Now).Should().Be(PromotionStatus.Scheduled);
        campaign.StatusAt(Now.AddDays(2)).Should().Be(PromotionStatus.Running);
        campaign.StatusAt(Now.AddDays(10)).Should().Be(PromotionStatus.Ended); // endsAt là mốc loại trừ
    }

    [Fact]
    public void StatusAt_SwitchedOffAndExhausted()
    {
        Campaign(isActive: false).StatusAt(Now).Should().Be(PromotionStatus.Inactive);
        Campaign(usageLimit: 3, usedCount: 3).StatusAt(Now).Should().Be(PromotionStatus.Exhausted);
        Campaign(usageLimit: 3, usedCount: 2).StatusAt(Now).Should().Be(PromotionStatus.Running);
    }

    [Fact]
    public void IsRunningAt_AgreesWithTheQueryExpression()
    {
        var campaigns = new[]
        {
            Campaign(id: 1),
            Campaign(id: 2, startsAt: Now.AddMinutes(1)),
            Campaign(id: 3, endsAt: Now),
            Campaign(id: 4, isActive: false),
            Campaign(id: 5, usageLimit: 1, usedCount: 1),
            Campaign(id: 6, usageLimit: 2, usedCount: 1, endsAt: Now.AddDays(1)),
        };
        var expression = PromotionCampaign.RunningAt(Now).Compile();

        foreach (var campaign in campaigns)
        {
            campaign.IsRunningAt(Now).Should().Be(expression(campaign), $"campaign {campaign.Id}");
        }

        campaigns.Where(campaign => campaign.IsRunningAt(Now)).Select(campaign => campaign.Id).Should().Equal(1, 6);
    }

    [Theory]
    [InlineData(nameof(DiscountType.Percent), 0)]
    [InlineData(nameof(DiscountType.Percent), 101)]
    [InlineData(nameof(DiscountType.FixedAmount), 0)]
    [InlineData(nameof(DiscountType.FixedAmount), -5)]
    public void Update_InvalidDiscountValue_Throws(string type, int value)
    {
        var act = () => Campaign(discountType: Enum.Parse<DiscountType>(type), value: value);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void Update_EndsBeforeStarts_Throws()
    {
        var act = () => Campaign(startsAt: Now, endsAt: Now);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Update_NonPositiveLimits_Throw()
    {
        var usage = () => Campaign(usageLimit: 0);
        var perCustomer = () => Campaign(perCustomerLimit: 0);

        usage.Should().Throw<BusinessException>();
        perCustomer.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Update_FreeShippingWithBookScope_Throws()
    {
        var act = () => Campaign(kind: PromotionKind.FreeShipping, scope: PromotionScopeType.Books, targets: [1]);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Update_FixedAmount_DropsMaxDiscountAmount()
    {
        var campaign = Campaign(discountType: DiscountType.FixedAmount, value: 50_000m, maxDiscount: 10_000m);

        campaign.MaxDiscountAmount.Should().BeNull();
    }

    [Fact]
    public void SetTargets_AllScopeRejectsTargets_OtherScopesRequireThem()
    {
        var all = () => Campaign(scope: PromotionScopeType.All, targets: [1]);
        var books = () => Campaign(scope: PromotionScopeType.Books, targets: []);
        var invalidId = () => Campaign(scope: PromotionScopeType.Books, targets: [0]);

        all.Should().Throw<BusinessException>();
        books.Should().Throw<BusinessException>();
        invalidId.Should().Throw<BusinessException>();
    }

    [Fact]
    public void SetTargets_KeepsExistingAndAppliesOnlyTheDifference()
    {
        var campaign = Campaign(scope: PromotionScopeType.Books, targets: [1, 2, 3]);

        campaign.SetTargets([2, 3, 4, 4]);

        campaign.Targets.Select(target => target.TargetId).Should().BeEquivalentTo([2L, 3L, 4L]);
    }

    [Fact]
    public void Usage_CountsUpAndNeverGoesNegative()
    {
        var campaign = Campaign(usedCount: 1);

        campaign.DecrementUsage();
        campaign.DecrementUsage();

        campaign.UsedCount.Should().Be(0);
    }

    [Fact]
    public void Coupon_NormalizesCodeToUpperCase()
    {
        var coupon = new Coupon(1, "  muathi-2026 ", usageLimit: null);

        coupon.Code.Should().Be("MUATHI-2026");
        coupon.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("co dau cach")]
    [InlineData("tiếng-việt")]
    public void Coupon_InvalidCode_Throws(string code)
    {
        var act = () => new Coupon(1, code, null);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Coupon_Exhausted_WhenUsedReachesLimit()
    {
        var coupon = new Coupon(1, "ONCE2026", usageLimit: 1);
        coupon.IsExhausted.Should().BeFalse();

        coupon.IncrementUsage();

        coupon.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void GeneratedCodes_AreValidUniqueLookingAndUnambiguous()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => CouponCodes.Generate("GT-")).ToList();

        codes.Should().OnlyContain(code => CouponCodes.IsValid(code) && code.StartsWith("GT-"));
        codes.Distinct().Count().Should().Be(codes.Count);
        char[] ambiguous = ['0', 'O', '1', 'I', 'L'];
        codes.Select(code => code[3..]).Should().OnlyContain(suffix => suffix.IndexOfAny(ambiguous) < 0);
    }

    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("+84901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("090 123 4567", "0901234567")]
    public void CustomerKey_TreatsPhoneFormatsAsTheSameCustomer(string phone, string expectedDigits)
    {
        PromotionCustomerKey.From(new PromotionCustomer(null, phone)).Should().Be($"p:{expectedDigits}");
    }

    [Fact]
    public void CustomerKey_PrefersAccountOverPhoneAndIsNullWhenUnknown()
    {
        PromotionCustomerKey.From(new PromotionCustomer(7, "0901234567")).Should().Be("u:7");
        PromotionCustomerKey.From(new PromotionCustomer(null, null)).Should().BeNull();
        PromotionCustomerKey.From(new PromotionCustomer(null, "  ")).Should().BeNull();
    }
}

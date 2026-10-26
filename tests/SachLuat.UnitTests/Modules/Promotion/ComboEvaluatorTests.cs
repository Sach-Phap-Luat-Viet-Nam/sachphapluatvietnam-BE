using AwesomeAssertions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using Xunit;
using static SachLuat.UnitTests.Modules.Promotion.PromotionTestData;

namespace SachLuat.UnitTests.Modules.Promotion;

/// <summary>
/// Ví dụ của chủ dự án: sách AB gồm tập 1 (A, SKU 1) và tập 2 (B, SKU 2), mua lẻ mỗi cuốn 50.000đ,
/// mua cả bộ chỉ 70.000đ.
/// </summary>
public sealed class ComboEvaluatorTests
{
    private static readonly PromotionLine[] BothVolumes = [Line(1, 50_000m), Line(2, 50_000m)];

    [Fact]
    public void FullSet_PaysTheComboPrice()
    {
        var result = Evaluate(Request(BothVolumes), [], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(30_000m); // 100.000đ lẻ − 70.000đ combo
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 15_000m, [2] = 15_000m });
        result.Applied.Should().ContainSingle().Which.Should().Match<AppliedPromotion>(applied =>
            applied.PromotionId == 5 && applied.Kind == PromotionKind.Combo && applied.Name == "Combo 5"
            && applied.CouponId == null && applied.CouponCode == null && applied.Amount == 30_000m);
        result.CouponApplied.Should().BeFalse();
    }

    [Fact]
    public void BuyingTheBooksSeparately_StillGetsTheComboPrice()
    {
        // Khách thêm lẻ từng quyển vào giỏ: giỏ có đủ SKU thì tự nhận diện, không cần bấm "mua combo".
        var result = Evaluate(Request([Line(2, 50_000m), Line(1, 50_000m)]), [], combos: [NewCombo()]);

        result.OrderDiscount.Should().Be(30_000m);
    }

    [Fact]
    public void MissingABook_AppliesNothing()
    {
        var result = Evaluate(Request([Line(1, 50_000m)]), [], combos: [NewCombo()]);

        result.Applied.Should().BeEmpty();
        result.OrderDiscount.Should().Be(0m);
        result.LineDiscounts.Should().BeEmpty();
    }

    [Fact]
    public void TwoFullSets_DiscountTwice()
    {
        var lines = new[] { Line(1, 50_000m, quantity: 2), Line(2, 50_000m, quantity: 2) };

        var result = Evaluate(Request(lines), [], combos: [NewCombo()]);

        result.OrderDiscount.Should().Be(60_000m);
        result.Applied.Should().ContainSingle().Which.Amount.Should().Be(60_000m);
    }

    [Fact]
    public void ExtraCopiesBeyondTheLastFullSet_PayFullPrice()
    {
        // 2 × A + 1 × B = một bộ đủ, quyển A thứ hai không được giảm.
        var lines = new[] { Line(1, 50_000m, quantity: 2), Line(2, 50_000m) };

        var result = Evaluate(Request(lines), [], combos: [NewCombo()]);

        result.OrderDiscount.Should().Be(30_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 15_000m, [2] = 15_000m });
    }

    [Fact]
    public void ItemQuantityPerSet_CountsTowardsTheNumberOfSets()
    {
        // Bộ gồm 2 × A + 1 × B, giá 120.000đ (lẻ 150.000đ) → mỗi bộ giảm 30.000đ.
        var combo = NewCombo(price: 120_000m, items: [(1, 2), (2, 1)]);

        var oneSet = Evaluate(
            Request([Line(1, 50_000m, quantity: 3), Line(2, 50_000m)]), [], combos: [combo]);
        var twoSets = Evaluate(
            Request([Line(1, 50_000m, quantity: 4), Line(2, 50_000m, quantity: 2)]), [], combos: [combo]);

        oneSet.OrderDiscount.Should().Be(30_000m);
        twoSets.OrderDiscount.Should().Be(60_000m);
    }

    [Fact]
    public void BooksAlreadyOnPriceSchedule_MakeTheComboPointless()
    {
        // Lẻ đang sale còn 60.000đ cho cả hai, rẻ hơn giá combo 70.000đ.
        var lines = new[] { Line(1, 30_000m, isOnSale: true), Line(2, 30_000m, isOnSale: true) };

        var result = Evaluate(Request(lines), [], combos: [NewCombo()]);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public void ComboPriceNotBelowRetail_AppliesNothing()
    {
        var result = Evaluate(Request(BothVolumes), [], combos: [NewCombo(price: 100_000m)]);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public void NotRunningCombos_AreIgnored()
    {
        var combos = new[]
        {
            NewCombo(id: 1, isActive: false),
            NewCombo(id: 2, startsAt: Now.AddMinutes(1)),
            NewCombo(id: 3, endsAt: Now), // endsAt là mốc loại trừ
        };

        var result = Evaluate(Request(BothVolumes), [], combos: combos);

        result.Applied.Should().BeEmpty();
    }

    [Fact]
    public void ComboBeatsASmallerOrderDiscount_AndReplacesIt()
    {
        // Sale 20% = 20.000đ < combo 30.000đ: không cộng dồn, lấy cái lợi hơn.
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 20m))], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(30_000m);
        result.Applied.Should().ContainSingle().Which.Kind.Should().Be(PromotionKind.Combo);
    }

    [Fact]
    public void BiggerOrderDiscountBeatsTheCombo()
    {
        // Sale 40% = 40.000đ > combo 30.000đ.
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 40m))], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(40_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 20_000m, [2] = 20_000m });
        result.Applied.Should().ContainSingle().Which.PromotionId.Should().Be(9);
    }

    [Fact]
    public void Tie_KeepsTheOrderDiscountProgram()
    {
        // 30% = 30.000đ bằng combo: giữ chương trình để hành vi cũ (mã khách nhập) không đổi.
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 30m))], combos: [NewCombo(id: 5)]);

        result.Applied.Should().ContainSingle().Which.Should().Match<AppliedPromotion>(applied =>
            applied.PromotionId == 9 && applied.Kind == PromotionKind.OrderDiscount);
    }

    [Fact]
    public void CouponLosingToTheCombo_IsNotAppliedAndDoesNotThrow()
    {
        var coupon = NewCoupon(promotionId: 2);

        var result = Evaluate(
            Request(BothVolumes, couponCode: "MUATHI2026"),
            [new PromotionCandidate(Campaign(id: 2, mode: PromotionMode.Coupon, value: 10m), coupon)],
            combos: [NewCombo(id: 5)]);

        result.Applied.Should().ContainSingle().Which.Kind.Should().Be(PromotionKind.Combo);
        result.CouponApplied.Should().BeFalse();
    }

    [Fact]
    public void ComboAndFreeShipping_AreAppliedTogether()
    {
        var result = Evaluate(
            Request(BothVolumes, shippingFee: 30_000m),
            [Auto(Campaign(id: 9, kind: PromotionKind.FreeShipping, value: 100m))],
            combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(30_000m);
        result.ShippingDiscount.Should().Be(30_000m);
        result.Applied.Select(applied => applied.Kind).Should()
            .Equal(PromotionKind.Combo, PromotionKind.FreeShipping);
    }

    [Fact]
    public void CombosSharingABook_BiggerSavingPerSetGetsTheBookFirst()
    {
        // Combo 1: A + B giá 70.000đ (giảm 30.000đ). Combo 2: B + C giá 60.000đ (giảm 40.000đ). Chỉ có một quyển B.
        var lines = new[] { Line(1, 50_000m), Line(2, 50_000m), Line(3, 50_000m) };
        var combos = new[] { NewCombo(id: 1), NewCombo(id: 2, price: 60_000m, items: [(2, 1), (3, 1)]) };

        var result = Evaluate(Request(lines), [], combos: combos);

        result.OrderDiscount.Should().Be(40_000m);
        result.Applied.Should().ContainSingle().Which.PromotionId.Should().Be(2);
    }

    [Fact]
    public void CombosSharingABook_BothApplyWhenThereAreEnoughCopies()
    {
        var lines = new[] { Line(1, 50_000m), Line(2, 50_000m, quantity: 2), Line(3, 50_000m) };
        var combos = new[] { NewCombo(id: 1), NewCombo(id: 2, price: 60_000m, items: [(2, 1), (3, 1)]) };

        var result = Evaluate(Request(lines), [], combos: combos);

        result.OrderDiscount.Should().Be(70_000m);
        result.Applied.Select(applied => applied.PromotionId).Should().Equal(2, 1);
        result.LineDiscounts.Values.Sum().Should().Be(70_000m);
    }

    [Fact]
    public void LineDiscounts_AlwaysSumToTheOrderDiscount_AndNeverExceedALineTotal()
    {
        // 33.333đ + 66.667đ = 100.000đ, giá combo 70.001đ → giảm 29.999đ, không chia hết theo tỷ lệ.
        var lines = new[] { Line(1, 33_333m), Line(2, 66_667m) };

        var result = Evaluate(Request(lines), [], combos: [NewCombo(price: 70_001m)]);

        result.OrderDiscount.Should().Be(29_999m);
        result.LineDiscounts.Values.Sum().Should().Be(29_999m);
        result.LineDiscounts[1].Should().BeLessThanOrEqualTo(33_333m);
        result.LineDiscounts[2].Should().BeLessThanOrEqualTo(66_667m);
    }

    [Fact]
    public void NoCombos_LeavesTheResultOfTheOrderDiscountUntouched()
    {
        var withoutCombos = Evaluate(Request(BothVolumes), [Auto(Campaign(value: 20m))]);
        var withEmptyCombos = Evaluate(Request(BothVolumes), [Auto(Campaign(value: 20m))], combos: []);

        withEmptyCombos.Should().BeEquivalentTo(withoutCombos);
        withoutCombos.OrderDiscount.Should().Be(20_000m);
    }
}

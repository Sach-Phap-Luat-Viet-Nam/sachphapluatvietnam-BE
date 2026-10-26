using AwesomeAssertions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Errors;
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
    public void OrderDiscount_StacksOnTopOfTheCombo()
    {
        // Combo giảm 30.000đ (còn 70.000đ), sale 20% tính tiếp trên 70.000đ = 14.000đ: tổng giảm 44.000đ.
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 20m))], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(44_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 22_000m, [2] = 22_000m });
        result.Applied.Select(applied => (applied.PromotionId, applied.Kind, applied.Amount)).Should().Equal(
            (5L, PromotionKind.Combo, 30_000m), (9L, PromotionKind.OrderDiscount, 14_000m));
    }

    [Fact]
    public void BiggerOrderDiscount_AlsoStacksInsteadOfReplacingTheCombo()
    {
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 40m))], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(58_000m); // 30.000đ combo + 40% × 70.000đ
        result.Applied.Select(applied => applied.Kind).Should()
            .Equal(PromotionKind.Combo, PromotionKind.OrderDiscount);
    }

    [Fact]
    public void Percent_IsComputedOnTheMoneyLeftAfterTheCombo_NotOnTheRetailTotal()
    {
        // 30% của 100.000đ lẻ là 30.000đ, nhưng tiền còn lại sau combo là 70.000đ nên chỉ giảm 21.000đ.
        var result = Evaluate(Request(BothVolumes), [Auto(Campaign(id: 9, value: 30m))], combos: [NewCombo(id: 5)]);

        result.Applied.Single(applied => applied.Kind == PromotionKind.OrderDiscount).Amount.Should().Be(21_000m);
        result.OrderDiscount.Should().Be(51_000m);
    }

    [Fact]
    public void FixedAmountProgram_NeverTakesMoreThanTheMoneyLeftAfterTheCombo()
    {
        var result = Evaluate(
            Request(BothVolumes),
            [Auto(Campaign(id: 9, discountType: DiscountType.FixedAmount, value: 100_000m))],
            combos: [NewCombo(id: 5)]);

        // Combo 30.000đ + chương trình tối đa 70.000đ còn lại: cả đơn về 0đ, không dòng nào bị giảm quá tiền của dòng.
        result.OrderDiscount.Should().Be(100_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 50_000m, [2] = 50_000m });
    }

    [Fact]
    public void MinOrder_StillUsesTheSubtotalBeforeTheCombo()
    {
        // Tạm tính 100.000đ trước combo (còn 70.000đ sau combo): chương trình đơn từ 100.000đ vẫn đủ điều kiện.
        var result = Evaluate(
            Request(BothVolumes), [Auto(Campaign(id: 9, value: 20m, minOrder: 100_000m))], combos: [NewCombo(id: 5)]);

        result.OrderDiscount.Should().Be(44_000m);
    }

    [Fact]
    public void ScopedProgram_OnlyReducesTheCoveredBooksAfterTheirShareOfTheCombo()
    {
        var result = Evaluate(
            Request(BothVolumes),
            [Auto(Campaign(id: 9, value: 20m, scope: PromotionScopeType.Books, targets: [1]))],
            combos: [NewCombo(id: 5)]);

        // Sách 1 còn 35.000đ sau combo: 20% = 7.000đ; sách 2 không thuộc phạm vi nên chỉ có phần giảm của combo.
        result.OrderDiscount.Should().Be(37_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 22_000m, [2] = 15_000m });
    }

    [Fact]
    public void Coupon_StacksWithTheCombo()
    {
        var coupon = NewCoupon(promotionId: 2);

        var result = Evaluate(
            Request(BothVolumes, couponCode: "MUATHI2026"),
            [new PromotionCandidate(Campaign(id: 2, mode: PromotionMode.Coupon, value: 10m), coupon)],
            combos: [NewCombo(id: 5)]);

        result.CouponApplied.Should().BeTrue();
        result.OrderDiscount.Should().Be(37_000m); // 30.000đ combo + 10% × 70.000đ
        result.Applied.Select(applied => (applied.Kind, applied.CouponCode)).Should().Equal(
            (PromotionKind.Combo, (string?)null), (PromotionKind.OrderDiscount, "MUATHI2026"));
    }

    [Fact]
    public void TotalDiscountCap_LimitsTheProgramOnBooksThatAlreadyHaveTheCombo()
    {
        // Giá gốc 2 × 550.000đ = 1.100.000đ, combo 575.000đ (giảm 525.000đ ~ 47,7%). Cộng thêm 10% sẽ vượt 50%, nên
        // chương trình chỉ được giảm thêm 25.000đ để tổng giảm đúng 550.000đ (50% giá bán thường).
        var lines = new[] { Line(1, 550_000m, regularPrice: 550_000m), Line(2, 550_000m, regularPrice: 550_000m) };

        var result = Evaluate(
            Request(lines),
            [Auto(Campaign(id: 9, value: 10m))],
            combos: [NewCombo(id: 5, price: 575_000m)],
            maxTotalDiscountPercent: 50);

        result.Applied.Single(applied => applied.Kind == PromotionKind.OrderDiscount).Amount.Should().Be(25_000m);
        result.OrderDiscount.Should().Be(550_000m);
        result.LineDiscounts.Should().BeEquivalentTo(new Dictionary<long, decimal> { [1] = 275_000m, [2] = 275_000m });
    }

    [Fact]
    public void TotalDiscountCap_DoesNotCountPriceSchedules_SoTheKhaiTruongCaseKeepsItsFullDiscount()
    {
        // Hai tập giá gốc 550.000đ đang giảm còn 290.000đ: combo 575.000đ rẻ hơn mua lẻ 5.000đ; "Khai trương" 5% tối đa
        // 25.000đ cộng thêm vẫn nằm xa trần 50% của giá bán thường. Khách trả 580.000đ − 30.000đ = 550.000đ.
        var lines = new[] { Line(1, 290_000m, regularPrice: 550_000m), Line(2, 290_000m, regularPrice: 550_000m) };

        var result = Evaluate(
            Request(lines),
            [Auto(Campaign(id: 9, value: 5m, maxDiscount: 25_000m, minOrder: 500_000m))],
            combos: [NewCombo(id: 5, price: 575_000m)],
            maxTotalDiscountPercent: 50);

        result.OrderDiscount.Should().Be(30_000m);
        result.Applied.Select(applied => (applied.Kind, applied.Amount)).Should().Equal(
            (PromotionKind.Combo, 5_000m), (PromotionKind.OrderDiscount, 25_000m));
    }

    [Fact]
    public void TotalDiscountCap_LeavesBooksWithoutAComboUnlimited()
    {
        // Sách 3 không thuộc combo nên không bị trần tổng giảm chặn: giữ nguyên hành vi cũ của chương trình.
        var lines = new[] { Line(1, 50_000m), Line(2, 50_000m), Line(3, 100_000m) };

        var result = Evaluate(
            Request(lines),
            [Auto(Campaign(id: 9, discountType: DiscountType.FixedAmount, value: 200_000m))],
            combos: [NewCombo(id: 5)],
            maxTotalDiscountPercent: 50);

        // Sách 1, 2 mỗi cuốn còn được giảm thêm 10.000đ (50% giá = 25.000đ, đã có 15.000đ của combo); sách 3 giảm hết.
        result.LineDiscounts.Should().BeEquivalentTo(
            new Dictionary<long, decimal> { [1] = 25_000m, [2] = 25_000m, [3] = 100_000m });
        result.OrderDiscount.Should().Be(150_000m);
    }

    [Fact]
    public void TotalDiscountCap_AlreadyReachedByTheCombo_SkipsAutomaticProgramsAndRejectsACoupon()
    {
        // Combo 50.000đ cho bộ 100.000đ đã giảm đúng 50%: không còn chỗ cho chương trình giảm trên đơn.
        var combo = NewCombo(id: 5, price: 50_000m);

        var automatic = Evaluate(
            Request(BothVolumes), [Auto(Campaign(id: 9, value: 10m))], combos: [combo], maxTotalDiscountPercent: 50);
        var couponProgram = Campaign(id: 2, mode: PromotionMode.Coupon, value: 10m);
        var withCoupon = () => Evaluate(
            Request(BothVolumes, couponCode: "MUATHI2026"),
            [new PromotionCandidate(couponProgram, NewCoupon(promotionId: 2))],
            combos: [combo],
            maxTotalDiscountPercent: 50);

        automatic.Applied.Should().ContainSingle().Which.Kind.Should().Be(PromotionKind.Combo);
        automatic.OrderDiscount.Should().Be(50_000m);
        withCoupon.Should().Throw<BusinessException>().Which.Code.Should().Be("COUPON_NOT_APPLICABLE");
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

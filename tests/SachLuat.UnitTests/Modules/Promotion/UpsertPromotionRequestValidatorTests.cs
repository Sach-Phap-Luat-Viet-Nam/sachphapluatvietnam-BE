using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Promotion.Application;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Promotion;

public sealed class UpsertPromotionRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7));

    private static UpsertPromotionRequestValidator Validator(int maxPercent = 50) =>
        new(Options.Create(new PromotionOptions { MaxDiscountPercent = maxPercent }));

    [Fact]
    public void TwentyPercentOffEveryBookUntilEndOfYear_IsValid()
    {
        // Ví dụ của chủ dự án: giảm 20% tổng hóa đơn, áp đến hết 31/12/2026.
        var request = Request() with { EndsAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(7)) };

        var result = Validator().Validate(request);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void OrderDiscountPercent_AboveConfiguredCap_IsInvalid()
    {
        var result = Validator(maxPercent: 50).Validate(Request() with { DiscountValue = 60m });

        result.Errors.Should().Contain(error => error.PropertyName == "DiscountValue");
    }

    [Fact]
    public void FreeShipping_MayBeHundredPercentDespiteTheCap()
    {
        var request = Request() with { Kind = PromotionKind.FreeShipping, DiscountValue = 100m };

        Validator(maxPercent: 50).Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void FixedAmount_MustBeWholeDong()
    {
        var request = Request() with { DiscountType = DiscountType.FixedAmount, DiscountValue = 10_000.5m };

        Validator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MaxDiscountAmount_OnlyForPercent()
    {
        var request = Request() with
        {
            DiscountType = DiscountType.FixedAmount, DiscountValue = 50_000m, MaxDiscountAmount = 10_000m,
        };

        Validator().Validate(request).Errors.Should().Contain(error => error.PropertyName == "MaxDiscountAmount");
    }

    [Fact]
    public void Scope_AllWithTargets_OrBooksWithoutTargets_IsInvalid()
    {
        var allWithTargets = Request() with { TargetIds = [1] };
        var booksWithout = Request() with { ScopeType = PromotionScopeType.Books, TargetIds = [] };

        Validator().Validate(allWithTargets).IsValid.Should().BeFalse();
        Validator().Validate(booksWithout).IsValid.Should().BeFalse();
        Validator().Validate(booksWithout with { TargetIds = [3, 4] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void FreeShipping_WithBookScopeOrExcludeOnSale_IsInvalid()
    {
        var scoped = Request() with
        {
            Kind = PromotionKind.FreeShipping, ScopeType = PromotionScopeType.Books, TargetIds = [1],
        };
        var excluding = Request() with { Kind = PromotionKind.FreeShipping, ExcludeOnSaleItems = true };

        Validator().Validate(scoped).IsValid.Should().BeFalse();
        Validator().Validate(excluding).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EndsAt_MustBeAfterStartsAt()
    {
        var result = Validator().Validate(Request() with { EndsAt = Start });

        result.Errors.Should().Contain(error => error.PropertyName == "EndsAt");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Limits_MustBePositive(int limit)
    {
        var result = Validator().Validate(Request() with { UsageLimit = limit, PerCustomerLimit = limit });

        result.Errors.Select(error => error.PropertyName).Should().Contain(["UsageLimit", "PerCustomerLimit"]);
    }

    [Fact]
    public void RequiredTexts_AreChecked()
    {
        var result = Validator().Validate(Request() with { Name = "", Title = new string('x', 256) });

        result.Errors.Select(error => error.PropertyName).Should().Contain(["Name", "Title"]);
    }

    [Fact]
    public void CouponRequests_AreChecked()
    {
        var options = Options.Create(new PromotionOptions { MaxCouponsPerBatch = 100 });

        new CreateCouponRequestValidator().Validate(new CreateCouponRequest("ok-2026", null)).IsValid.Should().BeTrue();
        new CreateCouponRequestValidator().Validate(new CreateCouponRequest("a b", null)).IsValid.Should().BeFalse();
        new GenerateCouponsRequestValidator(options).Validate(new GenerateCouponsRequest(100, "GT", 1))
            .IsValid.Should().BeTrue();
        new GenerateCouponsRequestValidator(options).Validate(new GenerateCouponsRequest(101, null, null))
            .IsValid.Should().BeFalse();
        new GenerateCouponsRequestValidator(options).Validate(new GenerateCouponsRequest(5, "co dau", null))
            .IsValid.Should().BeFalse();
    }

    private static UpsertPromotionRequest Request() => new(
        Name: "Sale cuối năm",
        Title: "Giảm ngay 20% trên tổng hóa đơn",
        Description: "Áp dụng cho tất cả các đầu sách pháp luật chính hãng.",
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
        Start,
        EndsAt: null,
        IsActive: true,
        Priority: 10,
        IsPublic: true);
}

using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Promotion.Application;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Promotion;

public sealed class UpsertComboRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7));

    private static readonly UpsertComboRequestValidator Validator = new();

    [Fact]
    public void TwoVolumesForSeventyThousand_IsValid()
    {
        var result = Validator.Validate(Request());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void Items_OutsideTwoToTenRows_AreInvalid(int count)
    {
        var items = Enumerable.Range(1, count).Select(index => new ComboItemRequest(index, 1)).ToList();

        Validator.Validate(Request() with { Items = items }).Errors
            .Should().Contain(error => error.PropertyName == "Items");
    }

    [Fact]
    public void Items_MissingOrDuplicateSku_AreInvalid()
    {
        Validator.Validate(Request() with { Items = null }).IsValid.Should().BeFalse();
        Validator.Validate(Request() with { Items = [new ComboItemRequest(1, 1), new ComboItemRequest(1, 2)] })
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 21)]
    public void ItemRow_SkuAndQuantity_AreChecked(long skuId, int quantity)
    {
        var request = Request() with { Items = [new ComboItemRequest(skuId, quantity), new ComboItemRequest(2, 1)] };

        Validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Combo ôn thi")]
    [InlineData("combo--on-thi")]
    [InlineData("")]
    public void Slug_MustBeLowercaseWordsJoinedByDashes(string slug)
    {
        Validator.Validate(Request() with { Slug = slug }).Errors
            .Should().Contain(error => error.PropertyName == "Slug");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70_000.5)]
    [InlineData(1_000_000_001)]
    public void ComboPrice_MustBeWholeDongAboveZero(double price)
    {
        Validator.Validate(Request() with { ComboPrice = (decimal)price }).Errors
            .Should().Contain(error => error.PropertyName == "ComboPrice");
    }

    [Fact]
    public void ImageUrl_MustBeHttps_WhenGiven()
    {
        Validator.Validate(Request() with { ImageUrl = "https://res.cloudinary.com/x/image/upload/combo.jpg" })
            .IsValid.Should().BeTrue();
        Validator.Validate(Request() with { ImageUrl = "http://example.com/combo.jpg" }).IsValid.Should().BeFalse();
        Validator.Validate(Request() with { ImageUrl = "combo.jpg" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EndsAt_MustBeAfterStartsAt()
    {
        Validator.Validate(Request() with { EndsAt = Start }).Errors
            .Should().Contain(error => error.PropertyName == "EndsAt");
    }

    [Fact]
    public void RequiredTexts_AreChecked()
    {
        var result = Validator.Validate(Request() with { Name = "", Description = new string('x', 1001) });

        result.Errors.Select(error => error.PropertyName).Should().Contain(["Name", "Description"]);
    }

    [Fact]
    public void PromotionRequest_CannotUseTheComboKind()
    {
        var validator = new UpsertPromotionRequestValidator(Options.Create(new PromotionOptions()));
        var request = new UpsertPromotionRequest(
            "Sale",
            "Giảm 20%",
            null,
            PromotionKind.Combo,
            PromotionMode.Automatic,
            DiscountType.Percent,
            20m,
            null,
            0m,
            PromotionScopeType.All,
            null,
            false,
            PromotionAudience.Everyone,
            null,
            null,
            Start,
            null,
            true,
            0,
            true);

        validator.Validate(request).Errors.Should().Contain(error => error.PropertyName == "Kind");
    }

    private static UpsertComboRequest Request() => new(
        Name: "Combo bộ sách AB",
        Slug: "combo-bo-sach-ab",
        Description: "Trọn bộ hai tập, tiết kiệm 30.000đ so với mua lẻ.",
        ImageUrl: null,
        ComboPrice: 70_000m,
        Items: [new ComboItemRequest(1, 1), new ComboItemRequest(2, 1)],
        Start,
        EndsAt: null,
        IsActive: true);
}

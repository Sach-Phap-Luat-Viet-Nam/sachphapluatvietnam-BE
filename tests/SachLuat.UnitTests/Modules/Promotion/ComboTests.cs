using AwesomeAssertions;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;
using static SachLuat.UnitTests.Modules.Promotion.PromotionTestData;

namespace SachLuat.UnitTests.Modules.Promotion;

public sealed class ComboTests
{
    [Fact]
    public void Create_TrimsTextsAndKeepsTheItems()
    {
        var combo = new Combo(
            new ComboDetails("  Combo AB  ", "combo-ab", "  Bộ 2 tập  ", " ", 70_000m, Now, null, true),
            [new ComboItemSpec(1, 1), new ComboItemSpec(2, 3)]);

        combo.Name.Should().Be("Combo AB");
        combo.Description.Should().Be("Bộ 2 tập");
        combo.ImageUrl.Should().BeNull();
        combo.Items.Select(item => (item.SkuId, item.Quantity)).Should().Equal((1L, 1), (2L, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void SetItems_OutsideTwoToTenBooks_Throws(int count)
    {
        var items = Enumerable.Range(1, count).Select(index => new ComboItemSpec(index, 1));

        var act = () => NewCombo().SetItems(items);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void SetItems_DuplicateSku_Throws()
    {
        var act = () => NewCombo().SetItems([new ComboItemSpec(1, 1), new ComboItemSpec(1, 2)]);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void SetItems_QuantityOutsideOneToTwenty_Throws(int quantity)
    {
        var act = () => NewCombo().SetItems([new ComboItemSpec(1, quantity), new ComboItemSpec(2, 1)]);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void SetItems_KeepsTheKnownBooksUpdatesTheirQuantityAndDropsTheRest()
    {
        var combo = NewCombo(items: [(1, 1), (2, 1)]);
        var first = combo.Items.Single(item => item.SkuId == 1);

        combo.SetItems([new ComboItemSpec(1, 4), new ComboItemSpec(3, 1)]);

        combo.Items.Select(item => (item.SkuId, item.Quantity)).Should().BeEquivalentTo([(1L, 4), (3L, 1)]);
        combo.Items.Should().Contain(first); // cùng một dòng, EF Core cập nhật thay vì xóa rồi thêm
    }

    [Theory]
    [InlineData("Combo ôn thi")]
    [InlineData("combo_on_thi")]
    [InlineData("-combo")]
    [InlineData("combo-")]
    [InlineData("combo--on-thi")]
    [InlineData("")]
    public void Create_InvalidSlug_Throws(string slug)
    {
        var act = () => new Combo(
            new ComboDetails("Combo", slug, null, null, 70_000m, Now, null, true),
            [new ComboItemSpec(1, 1), new ComboItemSpec(2, 1)]);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Theory]
    [InlineData("combo-on-thi-luat-su")]
    [InlineData("combo2026")]
    [InlineData("a")]
    public void Slug_LowercaseWordsAndDigits_IsValid(string slug) => ComboSlug.IsValid(slug).Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70_000.5)]
    [InlineData(1_000_000_001)]
    public void Update_InvalidPrice_Throws(double price)
    {
        var act = () => NewCombo(price: (decimal)price);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void Update_EndsNotAfterStarts_Throws()
    {
        var act = () => NewCombo(startsAt: Now, endsAt: Now);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void StatusAt_FollowsTheSchedule()
    {
        var combo = NewCombo(startsAt: Now.AddDays(1), endsAt: Now.AddDays(10));

        combo.StatusAt(Now).Should().Be(PromotionStatus.Scheduled);
        combo.StatusAt(Now.AddDays(2)).Should().Be(PromotionStatus.Running);
        combo.StatusAt(Now.AddDays(10)).Should().Be(PromotionStatus.Ended); // endsAt là mốc loại trừ
        NewCombo(isActive: false).StatusAt(Now).Should().Be(PromotionStatus.Inactive);
    }

    [Fact]
    public void IsRunningAt_AgreesWithTheQueryExpression()
    {
        var combos = new[]
        {
            NewCombo(id: 1),
            NewCombo(id: 2, startsAt: Now.AddMinutes(1)),
            NewCombo(id: 3, endsAt: Now),
            NewCombo(id: 4, isActive: false),
            NewCombo(id: 5, endsAt: Now.AddDays(1)),
        };
        var expression = Combo.RunningAt(Now).Compile();

        foreach (var combo in combos)
        {
            combo.IsRunningAt(Now).Should().Be(expression(combo), $"combo {combo.Id}");
        }

        combos.Where(combo => combo.IsRunningAt(Now)).Select(combo => combo.Id).Should().Equal(1, 5);
    }

    [Fact]
    public void Campaign_CannotBeCreatedWithTheComboKind()
    {
        var act = () => Campaign(kind: PromotionKind.Combo);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }
}

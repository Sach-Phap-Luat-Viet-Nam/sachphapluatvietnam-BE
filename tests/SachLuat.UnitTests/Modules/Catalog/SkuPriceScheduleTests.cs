using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class SkuPriceScheduleTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CurrentPrice_InsideSchedule_IsSchedulePrice()
    {
        var sku = CreateSku();

        var schedule = sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), " Sale mùa thi ", Now);

        sku.CurrentPriceAt(Now).Should().Be(150_000m);
        sku.CurrentPriceAt(Now.AddDays(7).AddTicks(-1)).Should().Be(150_000m);
        sku.ActivePriceScheduleAt(Now).Should().BeSameAs(schedule);
        schedule.Note.Should().Be("Sale mùa thi");
        schedule.StatusAt(Now).Should().Be(PriceScheduleStatus.Running);
    }

    [Fact]
    public void CurrentPrice_BeforeStartOrFromEnd_IsRegularPrice()
    {
        var sku = CreateSku();
        var schedule = sku.AddPriceSchedule(150_000m, Now.AddDays(1), Now.AddDays(2), null, Now);

        sku.CurrentPriceAt(Now).Should().Be(199_000m);
        sku.CurrentPriceAt(Now.AddDays(2)).Should().Be(199_000m);
        schedule.StatusAt(Now).Should().Be(PriceScheduleStatus.Scheduled);
        schedule.StatusAt(Now.AddDays(2)).Should().Be(PriceScheduleStatus.Ended);
    }

    [Fact]
    public void CurrentPrice_RegularPriceLoweredBelowSchedule_UsesLowerRegularPrice()
    {
        var sku = CreateSku();
        sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);

        sku.ChangePrices(250_000m, 120_000m);

        sku.CurrentPriceAt(Now).Should().Be(120_000m);
        sku.ActivePriceScheduleAt(Now).Should().BeNull();
    }

    [Theory]
    [InlineData(199_000)] // bằng giá bán thường: không giảm gì
    [InlineData(260_000)]
    public void AddPriceSchedule_NotLowerThanRegularPrice_ThrowsPriceInvalid(double salePrice)
    {
        var sku = CreateSku();

        var act = () => sku.AddPriceSchedule((decimal)salePrice, Now, Now.AddDays(1), null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceInvalidCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(150_000.5)]
    public void AddPriceSchedule_NotWholePositiveVnd_ThrowsPriceInvalid(double salePrice)
    {
        var sku = CreateSku();

        var act = () => sku.AddPriceSchedule((decimal)salePrice, Now, Now.AddDays(1), null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceInvalidCode);
    }

    [Fact]
    public void AddPriceSchedule_EndNotAfterStart_ThrowsValidationError()
    {
        var sku = CreateSku();

        var act = () => sku.AddPriceSchedule(150_000m, Now.AddDays(1), Now.AddDays(1), null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public void AddPriceSchedule_AlreadyEnded_ThrowsValidationError()
    {
        var sku = CreateSku();

        var act = () => sku.AddPriceSchedule(150_000m, Now.AddDays(-2), Now, null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public void AddPriceSchedule_Overlapping_ThrowsOverlap()
    {
        var sku = CreateSku();
        sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);

        var act = () => sku.AddPriceSchedule(140_000m, Now.AddDays(6), Now.AddDays(10), null, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceScheduleOverlapCode);
    }

    [Fact]
    public void AddPriceSchedule_StartsWhenPreviousEnds_IsAllowed()
    {
        var sku = CreateSku();
        sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);

        sku.AddPriceSchedule(140_000m, Now.AddDays(7), Now.AddDays(10), null, Now);

        sku.CurrentPriceAt(Now.AddDays(7)).Should().Be(140_000m);
    }

    [Fact]
    public void UpdatePriceSchedule_ExtendsOwnPeriod_DoesNotCountAsOverlap()
    {
        var sku = CreateSku();
        var schedule = sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);

        sku.UpdatePriceSchedule(schedule.Id, 145_000m, Now, Now.AddDays(14), "Gia hạn", Now);

        schedule.EndsAt.Should().Be(Now.AddDays(14));
        sku.CurrentPriceAt(Now.AddDays(10)).Should().Be(145_000m);
    }

    [Fact]
    public void UpdateOrRemove_EndedSchedule_ThrowsEnded()
    {
        var sku = CreateSku();
        var schedule = sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);
        var later = Now.AddDays(8);

        var update = () => sku.UpdatePriceSchedule(schedule.Id, 140_000m, later, later.AddDays(1), null, later);
        var remove = () => sku.RemovePriceSchedule(schedule.Id, later);

        update.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceScheduleEndedCode);
        remove.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceScheduleEndedCode);
    }

    [Fact]
    public void RemovePriceSchedule_Running_StopsDiscountImmediately()
    {
        var sku = CreateSku();
        var schedule = sku.AddPriceSchedule(150_000m, Now, Now.AddDays(7), null, Now);

        sku.RemovePriceSchedule(schedule.Id, Now.AddDays(1));

        sku.PriceSchedules.Should().BeEmpty();
        sku.CurrentPriceAt(Now.AddDays(1)).Should().Be(199_000m);
    }

    private static Sku CreateSku() =>
        new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code)
            .AddEdition(new EditionPublishingInfo("9780306406157", "NXB Tư pháp", "123/QĐ-NXBTP"))
            .AddSku("SLVN-BLDS-2026-BM", CoverType.Paperback, 250_000m, 199_000m);
}

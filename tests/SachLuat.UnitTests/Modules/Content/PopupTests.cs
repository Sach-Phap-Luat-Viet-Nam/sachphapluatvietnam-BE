using AwesomeAssertions;
using SachLuat.Modules.Content.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Content;

public sealed class PopupTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(7);

    [Theory]
    [InlineData(false, 1, "Inactive")]
    [InlineData(true, -1, "Scheduled")]
    [InlineData(true, 0, "Running")]
    [InlineData(true, 3, "Running")]
    [InlineData(true, 7, "Ended")]
    public void StatusAt_FollowsActiveFlagAndSchedule(bool isActive, int dayOffset, string expected)
    {
        var popup = new Popup(Details(isActive: isActive));
        var now = Start.AddDays(dayOffset);

        popup.StatusAt(now).ToString().Should().Be(expected);
        Popup.ShowingAt(now).Compile()(popup).Should().Be(expected == "Running");
    }

    [Fact]
    public void WithoutEndDate_RunsUntilTurnedOff()
    {
        var popup = new Popup(Details() with { EndsAt = null });

        popup.StatusAt(Start.AddYears(5)).Should().Be(PopupStatus.Running);
        Popup.ShowingAt(Start.AddYears(5)).Compile()(popup).Should().BeTrue();
    }

    [Fact]
    public void Update_EndNotAfterStart_Throws()
    {
        var act = () => new Popup(Details() with { EndsAt = Start });

        act.Should().Throw<BusinessException>().Where(exception => exception.Code == ErrorCodes.ValidationError);
    }

    [Fact]
    public void Update_TrimsTextAndDropsBlankLink()
    {
        var popup = new Popup(Details() with { Title = "  Sale Tết  ", LinkUrl = "   " });

        popup.Title.Should().Be("Sale Tết");
        popup.LinkUrl.Should().BeNull();
    }

    private static PopupDetails Details(bool isActive = true) => new(
        "Sale Tết",
        "https://res.cloudinary.com/demo-cloud/image/upload/sachluat/popups/tet.jpg",
        "/khuyen-mai",
        Start,
        End,
        isActive,
        Priority: 10,
        PopupDisplayFrequency.OncePerDay);
}

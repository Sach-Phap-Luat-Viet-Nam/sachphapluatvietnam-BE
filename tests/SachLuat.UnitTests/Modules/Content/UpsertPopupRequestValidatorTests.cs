using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Domain;
using SachLuat.SharedKernel.Media;
using Xunit;

namespace SachLuat.UnitTests.Modules.Content;

public sealed class UpsertPopupRequestValidatorTests
{
    private const string OwnImage = "https://res.cloudinary.com/demo-cloud/image/upload/sachluat/popups/tet.jpg";
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7));

    private static readonly CloudinaryOptions Configured = new()
    {
        CloudName = "demo-cloud",
        ApiKey = "123456",
        ApiSecret = "test-secret",
    };

    [Theory]
    [InlineData("/")]
    [InlineData("/khuyen-mai")]
    [InlineData("/sach/bo-luat-dan-su-2026?utm_source=popup")]
    [InlineData("https://zalo.me/sachluat")]
    [InlineData("http://example.com/landing")]
    public void IsSafeLink_InternalPathOrHttpUrl_IsAccepted(string link)
    {
        UpsertPopupRequestValidator.IsSafeLink(link).Should().BeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("khuyen-mai")]
    [InlineData("ftp://example.com/file")]
    public void IsSafeLink_ScriptOrOtherHost_IsRejected(string link)
    {
        UpsertPopupRequestValidator.IsSafeLink(link).Should().BeFalse();
    }

    [Fact]
    public void ValidRequest_WithoutLinkOrEndDate_IsValid()
    {
        var result = Validate(Request() with { LinkUrl = null, EndsAt = null });

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void ImageOutsideOwnCloudinary_IsInvalid()
    {
        var result = Validate(Request() with { ImageUrl = "https://example.com/tet.jpg" });

        result.Errors.Should().ContainSingle(error => error.PropertyName == "ImageUrl");
    }

    [Fact]
    public void EndBeforeStart_IsInvalid()
    {
        var result = Validate(Request() with { EndsAt = Start.AddHours(-1) });

        result.Errors.Should().ContainSingle(error => error.PropertyName == "EndsAt");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(UpsertPopupRequestValidator.MaxPriority + 1)]
    public void PriorityOutOfRange_IsInvalid(int priority)
    {
        var result = Validate(Request() with { Priority = priority });

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Priority");
    }

    private static UpsertPopupRequest Request() => new(
        "Sale Tết",
        OwnImage,
        "/khuyen-mai",
        Start,
        Start.AddDays(7),
        IsActive: true,
        Priority: 10,
        PopupDisplayFrequency.OncePerDay);

    private static FluentValidation.Results.ValidationResult Validate(UpsertPopupRequest request) =>
        new UpsertPopupRequestValidator(Options.Create(Configured)).Validate(request);
}

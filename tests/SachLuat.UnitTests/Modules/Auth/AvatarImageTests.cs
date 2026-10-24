using AwesomeAssertions;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Media;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class AvatarImageTests
{
    private static readonly CloudinaryOptions Configured = new()
    {
        CloudName = "demo-cloud",
        ApiKey = "123456",
        ApiSecret = "test-secret",
    };

    [Fact]
    public void PublicId_IsStablePerUserInAvatarFolder()
    {
        var publicId = AvatarImage.PublicId(Configured, 42);

        publicId.Should().MatchRegex("^sachluat/avatars/42-[0-9a-f]{16}$");
        AvatarImage.PublicId(Configured, 42).Should().Be(publicId);
        AvatarImage.PublicId(Configured, 43).Should().NotEndWith(publicId[^16..]);
    }

    [Fact]
    public void PublicId_DependsOnSecret_SoCannotBeGuessedFromUserId()
    {
        var otherSecret = new CloudinaryOptions { CloudName = "demo-cloud", ApiKey = "123456", ApiSecret = "other" };

        AvatarImage.PublicId(otherSecret, 42).Should().NotBe(AvatarImage.PublicId(Configured, 42));
    }

    [Fact]
    public void AcceptsUrl_OnlyOwnAvatar()
    {
        var own = $"{Configured.ImageUrlPrefix}v1712345678/{AvatarImage.PublicId(Configured, 42)}.jpg";
        var someoneElse = $"{Configured.ImageUrlPrefix}v1712345678/{AvatarImage.PublicId(Configured, 43)}.jpg";

        AvatarImage.AcceptsUrl(Configured, 42, own).Should().BeTrue();
        AvatarImage.AcceptsUrl(Configured, 42, someoneElse).Should().BeFalse();
        AvatarImage.AcceptsUrl(Configured, 42, $"{Configured.ImageUrlPrefix}v1/sachluat/covers/bia.jpg")
            .Should().BeFalse();
    }

    [Fact]
    public void AcceptsUrl_CloudinaryNotConfigured_AcceptsAnyLink()
    {
        AvatarImage.AcceptsUrl(new CloudinaryOptions(), 42, "https://example.com/avatar.png").Should().BeTrue();
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1/sachluat/avatars/42-abc.jpg", true)]
    [InlineData("http://example.com/avatar.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("avatar.png", false)]
    [InlineData("", false)]
    public void SetAvatarRequest_RequiresHttpsLink(string avatarUrl, bool expected)
    {
        new SetAvatarRequestValidator().Validate(new SetAvatarRequest(avatarUrl)).IsValid.Should().Be(expected);
    }

    [Fact]
    public void SetAvatarRequest_TooLong_IsInvalid()
    {
        var avatarUrl = "https://example.com/" + new string('a', 500);

        new SetAvatarRequestValidator().Validate(new SetAvatarRequest(avatarUrl)).Errors
            .Should().Contain(error => error.PropertyName == "AvatarUrl");
    }
}

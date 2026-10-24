using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Media;
using Xunit;

namespace SachLuat.UnitTests.SharedKernel;

public sealed class CloudinaryUploadSignerTests
{
    [Fact]
    public void Sign_MatchesCloudinaryDocumentationExample()
    {
        var parameters = new Dictionary<string, string>
        {
            ["timestamp"] = "1315060510",
            ["public_id"] = "sample_image",
            ["eager"] = "w_400,h_300,c_pad|w_260,h_200,c_crop",
        };

        CloudinaryUploadSigner.Sign(parameters, "abcd").Should().Be("bfd09f95f331f558cbd1320e67aa8d488770583e");
    }

    [Fact]
    public void CreateSignature_SignsFolderFormatsAndTimestampWithoutExposingSecret()
    {
        var signer = CreateSigner(new CloudinaryOptions
        {
            CloudName = "demo-cloud",
            ApiKey = "123456",
            ApiSecret = "test-secret",
        });

        var response = signer.CreateSignature("sachluat/covers");

        response.UploadUrl.Should().Be("https://api.cloudinary.com/v1_1/demo-cloud/image/upload");
        response.ApiKey.Should().Be("123456");
        response.Timestamp.Should().Be(1_700_000_000);
        response.Folder.Should().Be("sachluat/covers");
        response.AllowedFormats.Should().Be(CloudinaryUploadSigner.AllowedFormats);
        response.Signature.Should().Be("a9f13e1da18b665c6658a667edd50b2055e70a9d");
        response.ToString().Should().NotContain("test-secret");
    }

    [Fact]
    public void CreateSignature_DifferentFolder_SignsThatFolder()
    {
        var options = new CloudinaryOptions { CloudName = "demo-cloud", ApiKey = "123456", ApiSecret = "test-secret" };
        var signer = CreateSigner(options);

        var popup = signer.CreateSignature(options.PopupFolder);

        popup.Folder.Should().Be("sachluat/popups");
        popup.Signature.Should().NotBe(signer.CreateSignature(options.CoverFolder).Signature);
    }

    [Fact]
    public void CreateReplaceSignature_SignsPublicIdOverwriteFormatsAndTimestamp()
    {
        var signer = CreateSigner(new CloudinaryOptions
        {
            CloudName = "demo-cloud",
            ApiKey = "123456",
            ApiSecret = "test-secret",
        });

        var response = signer.CreateReplaceSignature("sachluat/avatars/42-abc");

        response.UploadUrl.Should().Be("https://api.cloudinary.com/v1_1/demo-cloud/image/upload");
        response.PublicId.Should().Be("sachluat/avatars/42-abc");
        response.Overwrite.Should().BeTrue();
        response.Timestamp.Should().Be(1_700_000_000);
        // Đúng bộ tham số FE sẽ gửi: thiếu/thừa tham số nào thì Cloudinary từ chối chữ ký.
        response.Signature.Should().Be(CloudinaryUploadSigner.Sign(
            new Dictionary<string, string>
            {
                ["allowed_formats"] = CloudinaryUploadSigner.AllowedFormats,
                ["overwrite"] = "true",
                ["public_id"] = "sachluat/avatars/42-abc",
                ["timestamp"] = "1700000000",
            },
            "test-secret"));
        response.ToString().Should().NotContain("test-secret");
    }

    [Fact]
    public void CreateReplaceSignature_NotConfigured_Throws503()
    {
        var signer = CreateSigner(new CloudinaryOptions { CloudName = "demo-cloud" });

        var act = () => signer.CreateReplaceSignature("sachluat/avatars/42-abc");

        act.Should().Throw<BusinessException>()
            .Where(exception => exception.Code == MediaErrors.UploadNotConfiguredCode);
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1712345678/sachluat/avatars/42-abc.jpg", true)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/sachluat/avatars/42-abc.webp", true)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1/sachluat/avatars/43-abc.jpg", false)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1/sachluat/avatars/42-abc.gif", false)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/w_9999/sachluat/avatars/42-abc.jpg", false)]
    [InlineData("https://res.cloudinary.com/other-cloud/image/upload/v1/sachluat/avatars/42-abc.jpg", false)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1/sachluat/avatars/42-abc.jpg?x=1", false)]
    public void IsImageUrlOf_OnlyThatPublicIdOnOwnCloud(string url, bool expected)
    {
        var options = new CloudinaryOptions { CloudName = "demo-cloud", ApiKey = "123456", ApiSecret = "test-secret" };

        options.IsImageUrlOf(url, "sachluat/avatars/42-abc").Should().Be(expected);
    }

    [Fact]
    public void CreateSignature_NotConfigured_Throws503()
    {
        var signer = CreateSigner(new CloudinaryOptions { CloudName = "demo-cloud" });

        var act = () => signer.CreateSignature("sachluat/covers");

        act.Should().Throw<BusinessException>()
            .Where(exception => exception.Code == MediaErrors.UploadNotConfiguredCode
                && exception.StatusCode == StatusCodes.Status503ServiceUnavailable);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1/sachluat/popups/tet.jpg", true)]
    [InlineData("https://res.cloudinary.com/other-cloud/image/upload/tet.jpg", false)]
    [InlineData("https://example.com/tet.jpg", false)]
    public void AcceptsImageUrl_OnlyOwnCloudinaryWhenConfigured(string? url, bool expected)
    {
        var options = new CloudinaryOptions { CloudName = "demo-cloud", ApiKey = "123456", ApiSecret = "test-secret" };

        options.AcceptsImageUrl(url).Should().Be(expected);
        new CloudinaryOptions().AcceptsImageUrl(url).Should().BeTrue();
    }

    private static CloudinaryUploadSigner CreateSigner(CloudinaryOptions options) => new(
        Options.Create(options),
        new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)));
}

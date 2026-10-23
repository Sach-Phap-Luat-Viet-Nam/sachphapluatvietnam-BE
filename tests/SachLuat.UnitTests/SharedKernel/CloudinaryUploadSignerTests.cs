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

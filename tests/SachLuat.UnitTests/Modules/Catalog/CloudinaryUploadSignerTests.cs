using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Infrastructure.Cloudinary;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

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
    public void CreateCoverSignature_SignsFolderFormatsAndTimestampWithoutExposingSecret()
    {
        var signer = CreateSigner(new CloudinaryOptions
        {
            CloudName = "demo-cloud",
            ApiKey = "123456",
            ApiSecret = "test-secret",
        });

        var response = signer.CreateCoverSignature();

        response.UploadUrl.Should().Be("https://api.cloudinary.com/v1_1/demo-cloud/image/upload");
        response.ApiKey.Should().Be("123456");
        response.Timestamp.Should().Be(1_700_000_000);
        response.Folder.Should().Be("sachluat/covers");
        response.AllowedFormats.Should().Be(CloudinaryUploadSigner.AllowedFormats);
        response.Signature.Should().Be("a9f13e1da18b665c6658a667edd50b2055e70a9d");
        response.ToString().Should().NotContain("test-secret");
    }

    [Fact]
    public void CreateCoverSignature_NotConfigured_Throws503()
    {
        var signer = CreateSigner(new CloudinaryOptions { CloudName = "demo-cloud" });

        var act = () => signer.CreateCoverSignature();

        act.Should().Throw<BusinessException>()
            .Where(exception => exception.Code == CatalogErrors.UploadNotConfiguredCode
                && exception.StatusCode == StatusCodes.Status503ServiceUnavailable);
    }

    private static CloudinaryUploadSigner CreateSigner(CloudinaryOptions options) => new(
        Options.Create(options),
        new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)));
}

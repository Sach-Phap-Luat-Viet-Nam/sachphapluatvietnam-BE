using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.SharedKernel.Media;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class UpsertEditionRequestValidatorTests
{
    private static readonly CloudinaryOptions Configured = new()
    {
        CloudName = "demo-cloud",
        ApiKey = "123456",
        ApiSecret = "test-secret",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("https://res.cloudinary.com/demo-cloud/image/upload/v1700000000/sachluat/covers/pl233.jpg")]
    public void CoverImageUrl_EmptyOrOwnCloudinary_IsValid(string? coverImageUrl)
    {
        var result = Validate(Configured, coverImageUrl);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("https://example.com/pl233.jpg")]
    [InlineData("https://res.cloudinary.com/other-cloud/image/upload/pl233.jpg")]
    [InlineData("http://res.cloudinary.com/demo-cloud/image/upload/pl233.jpg")]
    public void CoverImageUrl_OutsideOwnCloudinary_IsInvalid(string coverImageUrl)
    {
        var result = Validate(Configured, coverImageUrl);

        result.Errors.Should().ContainSingle(error => error.PropertyName == "CoverImageUrl");
    }

    [Fact]
    public void CoverImageUrl_CloudinaryNotConfigured_AcceptsAnyUrl()
    {
        var result = Validate(new CloudinaryOptions(), "https://example.com/pl233.jpg");

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    private static FluentValidation.Results.ValidationResult Validate(
        CloudinaryOptions options,
        string? coverImageUrl) =>
        new UpsertEditionRequestValidator(Options.Create(options)).Validate(new UpsertEditionRequest(
            Isbn: "9786040000002",
            Publisher: "NXB Tài Chính",
            PublishingDecisionNo: "123/QĐ-NXBTC",
            Status: EditionStatus.OnSale,
            RegistrationNo: null,
            EditionName: null,
            PublishYear: 2026,
            UpdatedToText: null,
            UpdatedToDate: null,
            Pages: 400,
            Size: null,
            WeightGram: 800,
            CoverImageUrl: coverImageUrl,
            PreviewFileUrl: null,
            TableOfContents: null,
            ReleaseDate: null,
            LegalDocumentIds: null));
}

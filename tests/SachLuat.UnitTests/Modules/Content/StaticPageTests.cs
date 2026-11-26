using AwesomeAssertions;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Content;

public sealed class StaticPageTests
{
    private static StaticPageDetails Details() =>
        new("dieu-khoan-su-dung", "Điều khoản sử dụng", "Tóm tắt", "<p>Nội dung</p>", IsPublished: true);

    [Fact]
    public void Create_TrimsFieldsAndLowercasesSlug()
    {
        var page = new StaticPage(Details() with
        {
            Slug = " Dieu-Khoan-Su-Dung ",
            Title = "  Điều khoản sử dụng ",
            ContentHtml = " <p>Nội dung</p>\n",
        });

        page.Slug.Should().Be("dieu-khoan-su-dung");
        page.Title.Should().Be("Điều khoản sử dụng");
        page.ContentHtml.Should().Be("<p>Nội dung</p>");
        page.IsPublished.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankDescription_IsStoredAsNull(string? description)
    {
        new StaticPage(Details() with { Description = description }).Description.Should().BeNull();
    }

    [Fact]
    public void Create_BlankContent_Throws()
    {
        var act = () => new StaticPage(Details() with { ContentHtml = " " });

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public void Update_ReplacesAllFields()
    {
        var page = new StaticPage(Details());

        page.Update(new StaticPageDetails("huong-dan", "Hướng dẫn", null, "<p>Mới</p>", IsPublished: false));

        page.Slug.Should().Be("huong-dan");
        page.Title.Should().Be("Hướng dẫn");
        page.Description.Should().BeNull();
        page.ContentHtml.Should().Be("<p>Mới</p>");
        page.IsPublished.Should().BeFalse();
    }
}

public sealed class UpsertStaticPageRequestValidatorTests
{
    private static readonly UpsertStaticPageRequestValidator Validator = new();

    private static UpsertStaticPageRequest Request() =>
        new("chinh-sach-bao-mat", "Chính sách bảo mật", null, "<p>Nội dung</p>", IsPublished: true);

    [Fact]
    public void ValidRequest_IsValid()
    {
        var result = Validator.Validate(Request());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("chinh-sach-doi-tra")]
    [InlineData("faq")]
    [InlineData("huong-dan-mua-hang-2026")]
    public void Slug_KebabCase_IsAccepted(string slug)
    {
        Validator.Validate(Request() with { Slug = slug }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Chinh-Sach")]
    [InlineData("chính-sách")]
    [InlineData("chinh sach")]
    [InlineData("-chinh-sach")]
    [InlineData("chinh--sach")]
    [InlineData("chinh-sach/")]
    [InlineData("../admin")]
    public void Slug_NotKebabCase_IsRejected(string slug)
    {
        var result = Validator.Validate(Request() with { Slug = slug });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertStaticPageRequest.Slug));
    }

    [Fact]
    public void ContentHtml_Empty_IsRejected()
    {
        var result = Validator.Validate(Request() with { ContentHtml = "" });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertStaticPageRequest.ContentHtml));
    }

    [Fact]
    public void ContentHtml_OverMaxLength_IsRejected()
    {
        var result = Validator.Validate(Request() with
        {
            ContentHtml = new string('a', StaticPage.ContentMaxLength + 1),
        });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertStaticPageRequest.ContentHtml));
    }

    [Fact]
    public void Description_OverMaxLength_IsRejected()
    {
        var result = Validator.Validate(Request() with
        {
            Description = new string('a', StaticPage.DescriptionMaxLength + 1),
        });

        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertStaticPageRequest.Description));
    }
}

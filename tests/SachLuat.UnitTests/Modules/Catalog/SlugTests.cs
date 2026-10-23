using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class SlugTests
{
    [Theory]
    [InlineData("bo-luat-dan-su-2026")]
    [InlineData("luat-dat-dai")]
    [InlineData("a1")]
    public void IsValid_KebabCaseAscii_ReturnsTrue(string slug)
    {
        Slug.IsValid(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bo-Luat")]
    [InlineData("bộ-luật")]
    [InlineData("bo--luat")]
    [InlineData("-bo-luat")]
    [InlineData("bo luat")]
    public void IsValid_InvalidSlug_ReturnsFalse(string? slug)
    {
        Slug.IsValid(slug).Should().BeFalse();
    }
}

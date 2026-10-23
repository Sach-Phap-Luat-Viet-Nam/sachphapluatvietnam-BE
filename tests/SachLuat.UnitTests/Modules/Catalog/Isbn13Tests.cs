using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class Isbn13Tests
{
    [Theory]
    [InlineData("9780306406157", "9780306406157")]
    [InlineData("978-604-123-456-7", "9786041234567")]
    [InlineData("979 1234 5678 96", "9791234567896")]
    public void TryNormalize_ValidIsbn_ReturnsDigitsOnly(string input, string expected)
    {
        Isbn13.TryNormalize(input, out var isbn).Should().BeTrue();
        isbn.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("978030640615")] // 12 chữ số
    [InlineData("9780306406158")] // sai số kiểm tra
    [InlineData("9771234567890")] // tiền tố không phải 978/979
    [InlineData("97803064061X7")]
    public void TryNormalize_InvalidIsbn_ReturnsFalse(string? input)
    {
        Isbn13.TryNormalize(input, out var isbn).Should().BeFalse();
        isbn.Should().BeNull();
    }
}

using AwesomeAssertions;
using SachLuat.SharedKernel.Domain;
using Xunit;

namespace SachLuat.UnitTests.SharedKernel;

public sealed class PhoneNumbersTests
{
    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("+84901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData(" 090 123 4567 ", "0901234567")]
    [InlineData("0841234567", "0841234567")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_SameNumberInDifferentFormats_Matches(string? input, string expected)
    {
        PhoneNumbers.Normalize(input).Should().Be(expected);
    }
}

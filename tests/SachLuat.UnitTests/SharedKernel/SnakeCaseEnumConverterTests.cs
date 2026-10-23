using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.SharedKernel.Persistence;
using Xunit;

namespace SachLuat.UnitTests.SharedKernel;

public sealed class SnakeCaseEnumConverterTests
{
    [Theory]
    [InlineData(EditionStatus.OnSale, "on_sale")]
    [InlineData(EditionStatus.Draft, "draft")]
    [InlineData(LegalDocumentType.JointCircular, "joint_circular")]
    public void ToProvider_WritesSnakeCase(Enum value, string expected)
    {
        var text = value switch
        {
            EditionStatus status => SnakeCaseEnumConverter<EditionStatus>.ToProvider(status),
            LegalDocumentType type => SnakeCaseEnumConverter<LegalDocumentType>.ToProvider(type),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        text.Should().Be(expected);
    }

    [Fact]
    public void FromProvider_RoundTripsAllValues()
    {
        foreach (var status in Enum.GetValues<EditionStatus>())
        {
            var text = SnakeCaseEnumConverter<EditionStatus>.ToProvider(status);

            SnakeCaseEnumConverter<EditionStatus>.FromProvider(text).Should().Be(status);
        }
    }

    [Fact]
    public void FromProvider_UnknownValue_Throws()
    {
        var act = () => SnakeCaseEnumConverter<EditionStatus>.FromProvider("OnSale");

        act.Should().Throw<InvalidOperationException>();
    }
}

using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using SachLuat.SharedKernel.Configuration;
using Xunit;

namespace SachLuat.UnitTests.SharedKernel;

public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void GetRequiredConnectionString_Configured_ReturnsValue()
    {
        var configuration = BuildConfiguration("Host=localhost;Database=sachluat");

        configuration.GetRequiredConnectionString().Should().Be("Host=localhost;Database=sachluat");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRequiredConnectionString_Missing_Throws(string? value)
    {
        var configuration = BuildConfiguration(value);

        var act = () => configuration.GetRequiredConnectionString();

        act.Should().Throw<InvalidOperationException>().WithMessage("*'Default'*");
    }

    private static IConfiguration BuildConfiguration(string? connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
        .Build();
}

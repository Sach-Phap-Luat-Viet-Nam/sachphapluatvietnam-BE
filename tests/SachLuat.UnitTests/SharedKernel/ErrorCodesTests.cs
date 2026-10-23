using AwesomeAssertions;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.SharedKernel;

public sealed class ErrorCodesTests
{
    [Theory]
    [InlineData(400, ErrorCodes.ValidationError)]
    [InlineData(401, ErrorCodes.Unauthorized)]
    [InlineData(403, ErrorCodes.Forbidden)]
    [InlineData(404, ErrorCodes.NotFound)]
    [InlineData(429, ErrorCodes.TooManyRequests)]
    [InlineData(500, ErrorCodes.InternalError)]
    [InlineData(503, ErrorCodes.InternalError)]
    [InlineData(418, ErrorCodes.Unknown)]
    public void FromStatusCode_MapsToStableCode(int statusCode, string expected)
    {
        ErrorCodes.FromStatusCode(statusCode).Should().Be(expected);
    }
}

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests;

public sealed class SecurityPipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/api/v1/admin/books")]
    [InlineData("/api/v1/admin/orders")]
    [InlineData("/api/v1/admin/inventory/receipts")]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/me")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("code").GetString().Should().Be(ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task InvalidQuery_Returns400WithFieldErrors()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/books?limit=500&sort=random", TestContext.Current.CancellationToken);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("code").GetString().Should().Be(ErrorCodes.ValidationError);
        var errors = body.RootElement.GetProperty("errors");
        errors.TryGetProperty("limit", out _).Should().BeTrue(text);
        errors.TryGetProperty("sort", out _).Should().BeTrue(text);
    }
}

using System.Net;
using AwesomeAssertions;
using Xunit;

namespace SachLuat.IntegrationTests;

public sealed class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("https://sachphapluatvietnam.vercel.app")]
    [InlineData("https://admin-sachphapluatvietnam.vercel.app")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:3001")]
    public async Task Preflight_FromAllowedOrigin_AllowsWithCredentials(string origin)
    {
        using var client = factory.CreateClient();
        using var request = CreatePreflight(origin);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(origin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle()
            .Which.Should().Be("true");
    }

    [Fact]
    public async Task Preflight_FromUnknownOrigin_HasNoCorsHeaders()
    {
        using var client = factory.CreateClient();
        using var request = CreatePreflight("https://evil.example.com");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task SimpleRequest_FromAllowedOrigin_ReturnsAllowOriginHeader()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
            .Which.Should().Be("http://localhost:3000");
    }

    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/books");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}

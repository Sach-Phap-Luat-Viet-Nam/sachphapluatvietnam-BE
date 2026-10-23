using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SachLuat.Api.Security;
using Xunit;

namespace SachLuat.UnitTests.Api.Security;

public sealed class TrustedProxyMiddlewareTests
{
    private const string Secret = "unit-test-proxy-secret-at-least-32-chars";
    private static readonly IPAddress ProxyIp = IPAddress.Parse("10.0.0.5");

    [Fact]
    public async Task InvokeAsync_ValidSecret_UsesClientIpAndRemovesSecretHeader()
    {
        var context = CreateContext(Secret, "203.0.113.7");

        var nextCalled = await InvokeAsync(Secret, context);

        nextCalled.Should().BeTrue();
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
        context.Request.Headers.ContainsKey(TrustedProxyMiddleware.SecretHeader).Should().BeFalse();
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("")]
    public async Task InvokeAsync_InvalidSecret_KeepsConnectionIp(string sentSecret)
    {
        var context = CreateContext(sentSecret, "203.0.113.7");

        var nextCalled = await InvokeAsync(Secret, context);

        nextCalled.Should().BeTrue();
        context.Connection.RemoteIpAddress.Should().Be(ProxyIp);
        context.Request.Headers.ContainsKey(TrustedProxyMiddleware.SecretHeader).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SecretNotConfigured_IgnoresClientIp()
    {
        var context = CreateContext(string.Empty, "203.0.113.7");

        await InvokeAsync(string.Empty, context);

        context.Connection.RemoteIpAddress.Should().Be(ProxyIp);
    }

    [Fact]
    public async Task InvokeAsync_ClientIpNotAnIpAddress_KeepsConnectionIp()
    {
        var context = CreateContext(Secret, "not-an-ip");

        await InvokeAsync(Secret, context);

        context.Connection.RemoteIpAddress.Should().Be(ProxyIp);
    }

    private static DefaultHttpContext CreateContext(string secret, string clientIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ProxyIp;
        context.Request.Headers[TrustedProxyMiddleware.SecretHeader] = secret;
        context.Request.Headers[TrustedProxyMiddleware.ClientIpHeader] = clientIp;
        return context;
    }

    private static async Task<bool> InvokeAsync(string configuredSecret, HttpContext context)
    {
        var nextCalled = false;
        var middleware = new TrustedProxyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new TrustedProxyOptions { Secret = configuredSecret }),
            NullLogger<TrustedProxyMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return nextCalled;
    }
}

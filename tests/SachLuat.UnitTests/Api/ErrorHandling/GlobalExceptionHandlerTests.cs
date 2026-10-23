using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SachLuat.Api.ErrorHandling;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Api.ErrorHandling;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_BusinessException_WritesProblemDetailsWithCodeAndStatus()
    {
        var httpContext = CreateHttpContext();
        var handler = CreateHandler(httpContext);
        var exception = new BusinessException("OUT_OF_STOCK", "Sách đã hết hàng", StatusCodes.Status409Conflict);

        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var body = await ReadBodyAsync(httpContext);
        body.GetProperty("code").GetString().Should().Be("OUT_OF_STOCK");
        body.GetProperty("title").GetString().Should().Be("Sách đã hết hàng");
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status409Conflict);
        body.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var httpContext = CreateHttpContext();
        var handler = CreateHandler(httpContext);
        var exception = new InvalidOperationException("Npgsql connection password=secret failed");

        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = await ReadBodyAsync(httpContext);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.InternalError);
        body.GetRawText().Should().NotContain("secret");
    }

    [Fact]
    public async Task TryHandleAsync_RequestAbortedByClient_Returns499WithoutBody()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var httpContext = CreateHttpContext();
        httpContext.RequestAborted = cancellation.Token;
        var handler = CreateHandler(httpContext);

        var handled = await handler.TryHandleAsync(
            httpContext, new OperationCanceledException(), TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(499);
        httpContext.Response.Body.Length.Should().Be(0);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApiProblemDetails()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };
    }

    private static GlobalExceptionHandler CreateHandler(HttpContext httpContext) => new(
        httpContext.RequestServices.GetRequiredService<IProblemDetailsService>(),
        NullLogger<GlobalExceptionHandler>.Instance);

    private static async Task<JsonElement> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Api.ErrorHandling;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Theo quy ước của nginx: client đóng kết nối trước khi server trả lời.
    private const int ClientClosedRequest = 499;

    private const string InternalErrorTitle = "Đã có lỗi xảy ra, vui lòng thử lại sau";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {Method} {Path} was cancelled by the client",
                httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problemDetails = exception switch
        {
            BusinessException businessException => CreateProblemDetails(
                businessException.StatusCode, businessException.Code, businessException.Message),
            _ => CreateProblemDetails(
                StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, InternalErrorTitle),
        };

        if (exception is BusinessException)
        {
            logger.LogInformation("Business rule violated {ErrorCode} on {Method} {Path}",
                problemDetails.Extensions["code"], httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string code, string title) => new()
    {
        Status = statusCode,
        Title = title,
        Extensions = { ["code"] = code },
    };
}

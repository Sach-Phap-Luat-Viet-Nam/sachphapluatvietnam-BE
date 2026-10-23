using SachLuat.SharedKernel.Errors;

namespace SachLuat.Api.ErrorHandling;

internal static class ProblemDetailsExtensions
{
    /// <summary>
    /// Mọi lỗi (exception, validation, 404 của routing) đều trả <c>ProblemDetails</c>
    /// có <c>code</c> và <c>traceId</c> để frontend và đội vận hành tra cứu.
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problemDetails = context.ProblemDetails;
            var statusCode = problemDetails.Status ?? context.HttpContext.Response.StatusCode;

            problemDetails.Extensions.TryAdd("code", ErrorCodes.FromStatusCode(statusCode));
            problemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}

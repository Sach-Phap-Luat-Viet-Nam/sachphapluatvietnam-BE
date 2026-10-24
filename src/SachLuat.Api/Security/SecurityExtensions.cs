using System.Threading.RateLimiting;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Api.Security;

internal static class SecurityExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        var rateLimiting = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddOptions<TrustedProxyOptions>()
            .Bind(configuration.GetSection(TrustedProxyOptions.SectionName))
            .Validate(
                options => options.Secret.Length is 0 or >= TrustedProxyOptions.MinSecretLength,
                $"TrustedProxy:Secret must be empty or at least {TrustedProxyOptions.MinSecretLength} characters.")
            .ValidateOnStart();

        // RemoteIpAddress là IP thật của khách khi request đi qua proxy FE (xem TrustedProxyMiddleware).
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimiting.AuthPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
            options.AddPolicy(RateLimitPolicies.Checkout, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimiting.CheckoutPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });

        var cors = configuration.GetSection(ApiCorsOptions.SectionName).Get<ApiCorsOptions>() ?? new ApiCorsOptions();

        // Header Origin của trình duyệt không có "/" cuối, nên bỏ "/" để cấu hình dán nguyên URL vẫn khớp.
        var allowedOrigins = cors.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToArray();

        // AllowCredentials để cookie sl_refresh/sl_cart đi kèm request; vì vậy không được dùng AllowAnyOrigin.
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }
}

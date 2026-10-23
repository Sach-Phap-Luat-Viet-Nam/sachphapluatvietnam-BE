using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SachLuat.Modules.Cart.Infrastructure;

internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Ping {latency.TotalMilliseconds:F0} ms");
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable", exception);
        }
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SachLuat.Api.Security;

/// <summary>
/// Thay <see cref="ConnectionInfo.RemoteIpAddress"/> bằng IP do proxy FE gửi qua <see cref="ClientIpHeader"/>,
/// chỉ khi <see cref="SecretHeader"/> khớp secret cấu hình. Không dùng X-Forwarded-For vì Render nối thêm
/// giá trị client gửi lên thay vì ghi đè, nên khách gọi thẳng vào Render có thể giả IP.
/// </summary>
internal sealed class TrustedProxyMiddleware
{
    public const string ClientIpHeader = "X-Client-Ip";
    public const string SecretHeader = "X-Proxy-Secret";

    private readonly RequestDelegate _next;
    private readonly ILogger<TrustedProxyMiddleware> _logger;
    private readonly byte[] _secret;

    public TrustedProxyMiddleware(
        RequestDelegate next,
        IOptions<TrustedProxyOptions> options,
        ILogger<TrustedProxyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _secret = Encoding.UTF8.GetBytes(options.Value.Secret);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (_secret.Length > 0 && headers.TryGetValue(SecretHeader, out var secret))
        {
            if (!IsValidSecret(secret.ToString()))
            {
                _logger.LogWarning("Rejected {Header} with invalid secret", SecretHeader);
            }
            else if (IPAddress.TryParse(headers[ClientIpHeader].ToString(), out var clientIp))
            {
                context.Connection.RemoteIpAddress = clientIp;
            }
        }

        // Không để secret đi tiếp xuống pipeline (log request, controller).
        headers.Remove(SecretHeader);
        return _next(context);
    }

    private bool IsValidSecret(string secret) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), _secret);
}

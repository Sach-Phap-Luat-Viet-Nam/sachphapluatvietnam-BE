using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>HttpClient có cookie (refresh token, giỏ khách) và access token, gọi API qua HTTPS giả lập.</summary>
internal sealed class ApiClient : IDisposable
{
    private readonly HttpClient _client;

    public ApiClient(EndToEndFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    public string? AccessToken { get; set; }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public Task<JsonElement> GetAsync(string path, HttpStatusCode expected = HttpStatusCode.OK) =>
        SendAsync(HttpMethod.Get, path, body: null, expected);

    public Task<JsonElement> PostAsync(
        string path,
        object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK,
        string? idempotencyKey = null) => SendAsync(HttpMethod.Post, path, body, expected, idempotencyKey);

    public Task<JsonElement> PutAsync(string path, object body, HttpStatusCode expected = HttpStatusCode.OK) =>
        SendAsync(HttpMethod.Put, path, body, expected);

    public async Task<JsonElement> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        HttpStatusCode expected,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var response = await _client.SendAsync(request, CancellationToken);
        var text = await response.Content.ReadAsStringAsync(CancellationToken);
        response.StatusCode.Should().Be(expected, $"{method} {path} returned: {text}");
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Đăng nhập; nếu tài khoản đã bật 2FA thì nhập mã TOTP từ <paramref name="totpKey"/>.</summary>
    public async Task<JsonElement> LoginAsync(string email, string password, string? totpKey = null)
    {
        var login = await PostAsync("/api/v1/auth/login", new { email, password });
        var session = login.GetProperty("requiresTwoFactor").GetBoolean()
            ? await PostAsync("/api/v1/auth/login/2fa", new
            {
                twoFactorToken = login.GetProperty("twoFactorToken").GetString(),
                code = Totp.Compute(totpKey ?? throw new InvalidOperationException("TOTP key required")),
            })
            : login.GetProperty("session");
        AccessToken = session.GetProperty("accessToken").GetString();
        return session;
    }

    /// <summary>Bật 2FA cho tài khoản đang đăng nhập; trả khóa TOTP để đăng nhập lại về sau.</summary>
    public async Task<string> EnableTwoFactorAsync()
    {
        var setup = await PostAsync("/api/v1/auth/2fa/setup");
        var key = setup.GetProperty("sharedKey").GetString() ?? throw new InvalidOperationException("No key");
        var session = await PostAsync("/api/v1/auth/2fa/enable", new { code = Totp.Compute(key) });
        AccessToken = session.GetProperty("accessToken").GetString();
        return key;
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>TOTP RFC 6238 (HMAC-SHA1, 30 giây, 6 số) như ứng dụng authenticator, để test bước 2FA.</summary>
internal static class Totp
{
    public static string Compute(string base32Key)
    {
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = HMACSHA1.HashData(DecodeBase32(base32Key), counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string input)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input.TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | Alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Notification.Contracts;

namespace SachLuat.Modules.Notification.Infrastructure.Zalo;

/// <summary>Gửi tin vào group nội bộ qua Zalo Bot API (<c>POST /bot{token}/sendMessage</c>).</summary>
/// <remarks>
/// Token nằm trong đường dẫn theo quy định của Zalo Bot API, nên không bao giờ log URL request; logger mặc định của
/// HttpClient đã bị tắt khi đăng ký (xem <see cref="NotificationModule"/>).
/// </remarks>
internal sealed class ZaloBotStaffNotifier(
    HttpClient httpClient,
    IOptions<ZaloBotOptions> options,
    ILogger<ZaloBotStaffNotifier> logger) : IStaffNotifier
{
    /// <summary>Giới hạn độ dài <c>text</c> của sendMessage.</summary>
    internal const int MaxTextLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ZaloBotOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled && _options.IsConfigured;

    public async Task SendAsync(string text, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Zalo bot chưa được bật hoặc thiếu Zalo:BotToken/Zalo:ChatId");
        }

        try
        {
            // Tiền tố "./": token dạng "123:abc" có dấu ":" sẽ bị Uri hiểu nhầm thành scheme nếu đứng đầu.
            using var response = await httpClient.PostAsJsonAsync(
                $"./bot{_options.BotToken}/sendMessage",
                new SendMessageRequest(_options.ChatId, Truncate(text)),
                JsonOptions,
                cancellationToken);
            var body = await ReadBodyAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode || body is not { Ok: true })
            {
                throw new StaffNotificationException(
                    $"Zalo Bot API từ chối: HTTP {(int)response.StatusCode}, error_code {body?.ErrorCode}, "
                    + body?.Description);
            }
        }
        catch (HttpRequestException exception)
        {
            throw new StaffNotificationException("Không kết nối được Zalo Bot API", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StaffNotificationException("Zalo Bot API quá thời gian chờ", exception);
        }

        logger.LogDebug("Sent staff notification to Zalo chat");
    }

    internal static string Truncate(string text)
    {
        if (text.Length <= MaxTextLength)
        {
            return text;
        }

        // Chừa một ký tự cho dấu "…"; không cắt giữa cặp surrogate của emoji.
        var length = MaxTextLength - 1;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return string.Concat(text.AsSpan(0, length), "…");
    }

    private static async Task<ZaloBotResponse?> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ZaloBotResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Lỗi từ proxy/gateway thường trả HTML; chỉ cần mã HTTP để báo lỗi.
            return null;
        }
    }

    private sealed record SendMessageRequest(string ChatId, string Text);

    private sealed record ZaloBotResponse(bool Ok, int? ErrorCode, string? Description);
}

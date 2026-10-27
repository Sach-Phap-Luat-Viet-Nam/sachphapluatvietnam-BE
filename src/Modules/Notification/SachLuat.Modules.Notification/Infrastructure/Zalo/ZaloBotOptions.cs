using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Notification.Infrastructure.Zalo;

internal sealed class ZaloBotOptions
{
    public const string SectionName = "Zalo";

    /// <summary>Tắt (mặc định) thì không gửi và Ordering không xếp hàng thông báo đơn.</summary>
    public bool Enabled { get; set; }

    /// <summary>Token bot lấy từ Zalo Bot Manager. Là secret: đặt trong user-secrets/biến môi trường.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Id group nội bộ nhận tin (<c>message.chat.id</c> trong getUpdates/webhook), xem docs.</summary>
    [MaxLength(100)]
    public string ChatId { get; set; } = string.Empty;

    [Url]
    public string BaseUrl { get; set; } = "https://bot-api.zaloplatforms.com";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}

using System.Linq.Expressions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Content.Domain;

/// <summary>Popup quảng cáo trên Storefront (FR-91).</summary>
internal sealed class Popup : AuditableEntity, ISoftDeletable
{
    public const int TitleMaxLength = 255;
    public const int UrlMaxLength = 500;

    public Popup(PopupDetails details) => Update(details);

    // EF Core
    private Popup()
    {
    }

    /// <summary>Tên chiến dịch, đồng thời là alt text của ảnh.</summary>
    public string Title { get; private set; } = string.Empty;

    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>Link khi bấm vào popup; null thì chỉ hiển thị ảnh.</summary>
    public string? LinkUrl { get; private set; }

    public DateTime StartsAt { get; private set; }

    /// <summary>null: chạy đến khi tắt.</summary>
    public DateTime? EndsAt { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Nhiều popup cùng chạy thì Storefront lấy popup có số lớn nhất.</summary>
    public int Priority { get; private set; }

    public PopupDisplayFrequency DisplayFrequency { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    /// <summary>Popup bật và đang trong lịch chạy tại <paramref name="now"/> (UTC); dùng được trong truy vấn EF.</summary>
    public static Expression<Func<Popup, bool>> ShowingAt(DateTime now) =>
        popup => popup.IsActive && popup.StartsAt <= now && (popup.EndsAt == null || popup.EndsAt > now);

    public void Update(PopupDetails details)
    {
        if (details.EndsAt is { } endsAt && endsAt <= details.StartsAt)
        {
            throw ContentErrors.Invalid("Thời gian kết thúc phải sau thời gian bắt đầu");
        }

        Title = Required(details.Title, "tiêu đề");
        ImageUrl = Required(details.ImageUrl, "ảnh");
        LinkUrl = string.IsNullOrWhiteSpace(details.LinkUrl) ? null : details.LinkUrl.Trim();
        StartsAt = details.StartsAt;
        EndsAt = details.EndsAt;
        IsActive = details.IsActive;
        Priority = details.Priority;
        DisplayFrequency = details.DisplayFrequency;
    }

    public PopupStatus StatusAt(DateTime now)
    {
        if (!IsActive)
        {
            return PopupStatus.Inactive;
        }

        if (now < StartsAt)
        {
            return PopupStatus.Scheduled;
        }

        return EndsAt <= now ? PopupStatus.Ended : PopupStatus.Running;
    }

    private static string Required(string? value, string fieldLabel) =>
        string.IsNullOrWhiteSpace(value) ? throw ContentErrors.Invalid($"Thiếu {fieldLabel}") : value.Trim();
}

/// <param name="StartsAt">UTC.</param>
/// <param name="EndsAt">UTC.</param>
internal sealed record PopupDetails(
    string Title,
    string ImageUrl,
    string? LinkUrl,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    PopupDisplayFrequency DisplayFrequency);

/// <summary>Tần suất hiện lại popup với cùng một khách; FE tự áp dụng (lưu dấu đã xem ở trình duyệt).</summary>
internal enum PopupDisplayFrequency
{
    EveryVisit,
    OncePerSession,
    OncePerDay,
}

/// <summary>Trạng thái tính theo thời điểm hiện tại, chỉ để hiển thị trên Admin (không lưu).</summary>
internal enum PopupStatus
{
    Inactive,
    Scheduled,
    Running,
    Ended,
}

using SachLuat.Modules.Content.Domain;

namespace SachLuat.Modules.Content.Application;

/// <param name="StartsAt">Có offset (ví dụ <c>2026-11-01T00:00:00+07:00</c>); lưu UTC.</param>
/// <param name="EndsAt">null: chạy đến khi tắt.</param>
internal sealed record UpsertPopupRequest(
    string Title,
    string ImageUrl,
    string? LinkUrl,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    int Priority,
    PopupDisplayFrequency DisplayFrequency);

internal sealed record AdminPopupResponse(
    long Id,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    PopupDisplayFrequency DisplayFrequency,
    PopupStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <param name="UpdatedAt">FE ghép với <see cref="Id"/> làm khóa "đã xem", để popup vừa sửa được hiện lại.</param>
internal sealed record PopupResponse(
    long Id,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    PopupDisplayFrequency DisplayFrequency,
    DateTime UpdatedAt);

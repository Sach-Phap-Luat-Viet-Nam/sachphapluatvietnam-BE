namespace SachLuat.Modules.Content.Application;

internal sealed record UpsertStaticPageRequest(
    string Slug,
    string Title,
    string? Description,
    string ContentHtml,
    bool IsPublished);

/// <summary>Một dòng trong danh sách Admin, không kèm nội dung.</summary>
internal sealed record AdminStaticPageSummaryResponse(
    long Id,
    string Slug,
    string Title,
    bool IsPublished,
    DateTime UpdatedAt);

internal sealed record AdminStaticPageResponse(
    long Id,
    string Slug,
    string Title,
    string? Description,
    string ContentHtml,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <param name="ContentHtml">HTML Admin soạn, chưa lọc: nơi render phải sanitize.</param>
/// <param name="UpdatedAt">Lần sửa gần nhất (UTC), Storefront hiển thị làm ngày cập nhật.</param>
internal sealed record StaticPageResponse(
    string Slug,
    string Title,
    string? Description,
    string ContentHtml,
    DateTime UpdatedAt);

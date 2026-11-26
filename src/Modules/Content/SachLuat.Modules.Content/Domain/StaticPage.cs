using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Content.Domain;

/// <summary>Trang tĩnh trên Storefront: điều khoản, chính sách, hướng dẫn... (FR-51, FR-91).</summary>
internal sealed class StaticPage : AuditableEntity
{
    public const int SlugMaxLength = 100;
    public const int TitleMaxLength = 255;
    public const int DescriptionMaxLength = 500;

    /// <summary>Đủ cho một văn bản chính sách dài; chặn request quá lớn chứ không phải giới hạn nghiệp vụ.</summary>
    public const int ContentMaxLength = 200_000;

    public StaticPage(StaticPageDetails details) => Update(details);

    // EF Core
    private StaticPage()
    {
    }

    /// <summary>Đường dẫn trên Storefront, chữ thường kebab-case (ví dụ <c>dieu-khoan-su-dung</c>).</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Tóm tắt ngắn dưới tiêu đề, đồng thời là meta description.</summary>
    public string? Description { get; private set; }

    /// <summary>HTML do Admin soạn. Lưu nguyên văn; Storefront lọc (sanitize) trước khi render.</summary>
    public string ContentHtml { get; private set; } = string.Empty;

    /// <summary>false: chỉ Admin thấy, API công khai trả 404.</summary>
    public bool IsPublished { get; private set; }

    public void Update(StaticPageDetails details)
    {
        Slug = Required(details.Slug, "đường dẫn").ToLowerInvariant();
        Title = Required(details.Title, "tiêu đề");
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        ContentHtml = Required(details.ContentHtml, "nội dung");
        IsPublished = details.IsPublished;
    }

    private static string Required(string? value, string fieldLabel) =>
        string.IsNullOrWhiteSpace(value) ? throw ContentErrors.Invalid($"Thiếu {fieldLabel}") : value.Trim();
}

internal sealed record StaticPageDetails(
    string Slug,
    string Title,
    string? Description,
    string ContentHtml,
    bool IsPublished);

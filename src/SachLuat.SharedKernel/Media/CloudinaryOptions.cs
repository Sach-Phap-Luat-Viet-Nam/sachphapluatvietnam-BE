namespace SachLuat.SharedKernel.Media;

/// <summary>Tài khoản Cloudinary dùng chung cho mọi module lưu ảnh (section <c>Cloudinary</c>).</summary>
public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Bí mật, chỉ dùng để ký ở server, không bao giờ trả về client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Thư mục ảnh bìa ấn bản (Catalog).</summary>
    public string CoverFolder { get; set; } = "sachluat/covers";

    /// <summary>Thư mục ảnh popup quảng cáo (Content).</summary>
    public string PopupFolder { get; set; } = "sachluat/popups";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>Tiền tố của mọi link ảnh (<c>secure_url</c>) Cloudinary trả về cho tài khoản này.</summary>
    public string ImageUrlPrefix => $"https://res.cloudinary.com/{CloudName}/image/upload/";

    /// <summary>
    /// Link ảnh nằm trên Cloudinary của hệ thống. Link rỗng, hoặc chưa cấu hình Cloudinary (local/test),
    /// thì coi là hợp lệ để vẫn nhập liệu được.
    /// </summary>
    public bool AcceptsImageUrl(string? url) =>
        !IsConfigured
        || string.IsNullOrWhiteSpace(url)
        || url.StartsWith(ImageUrlPrefix, StringComparison.Ordinal);
}

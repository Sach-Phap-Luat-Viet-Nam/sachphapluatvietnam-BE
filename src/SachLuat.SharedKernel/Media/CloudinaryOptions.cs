using System.Text.RegularExpressions;

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

    /// <summary>Thư mục avatar người dùng (Auth).</summary>
    public string AvatarFolder { get; set; } = "sachluat/avatars";

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

    /// <summary>
    /// Link (<c>secure_url</c>) là đúng ảnh <paramref name="publicId"/> trên Cloudinary của hệ thống, có hoặc không có
    /// đoạn version <c>v123/</c>, đuôi là một định dạng được phép upload.
    /// </summary>
    public bool IsImageUrlOf(string url, string publicId)
    {
        var formats = CloudinaryUploadSigner.AllowedFormats.Replace(',', '|');
        var pattern = $"^{Regex.Escape(ImageUrlPrefix)}(v[0-9]+/)?{Regex.Escape(publicId)}\\.({formats})$";
        return Regex.IsMatch(url, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}

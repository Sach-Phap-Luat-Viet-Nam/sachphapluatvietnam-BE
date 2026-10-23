namespace SachLuat.Modules.Catalog.Infrastructure.Cloudinary;

internal sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Bí mật, chỉ dùng để ký ở server, không bao giờ trả về client.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    public string CoverFolder { get; set; } = "sachluat/covers";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>Tiền tố của mọi link ảnh (<c>secure_url</c>) Cloudinary trả về cho tài khoản này.</summary>
    public string ImageUrlPrefix => $"https://res.cloudinary.com/{CloudName}/image/upload/";
}

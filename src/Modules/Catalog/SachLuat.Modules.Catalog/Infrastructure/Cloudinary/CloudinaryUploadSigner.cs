using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Domain;

namespace SachLuat.Modules.Catalog.Infrastructure.Cloudinary;

/// <summary>
/// Ký tham số upload để Admin FE gửi ảnh thẳng lên Cloudinary, không đi qua API.
/// Cloudinary chỉ nhận chữ ký có <c>timestamp</c> trong vòng 1 giờ.
/// </summary>
internal sealed class CloudinaryUploadSigner(IOptions<CloudinaryOptions> options, TimeProvider timeProvider)
{
    public const string AllowedFormats = "jpg,jpeg,png,webp";

    public UploadSignatureResponse CreateCoverSignature()
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw CatalogErrors.UploadNotConfigured();
        }

        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var parameters = new Dictionary<string, string>
        {
            ["allowed_formats"] = AllowedFormats,
            ["folder"] = settings.CoverFolder,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
        };

        return new UploadSignatureResponse(
            $"https://api.cloudinary.com/v1_1/{settings.CloudName}/image/upload",
            settings.ApiKey,
            timestamp,
            settings.CoverFolder,
            AllowedFormats,
            Sign(parameters, settings.ApiSecret));
    }

    /// <summary>
    /// Thuật toán của Cloudinary: tham số sắp theo tên, nối <c>key=value</c> bằng <c>&amp;</c>, thêm api_secret
    /// ở cuối rồi băm SHA-1 (thuật toán mặc định của tài khoản Cloudinary).
    /// </summary>
    internal static string Sign(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        var payload = string.Join(
            '&',
            parameters.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}={parameter.Value}"));

        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(payload + apiSecret)));
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.SharedKernel.Media;

/// <summary>Các field FE gửi kèm file (multipart) lên <see cref="UploadUrl"/>.</summary>
public sealed record UploadSignatureResponse(
    string UploadUrl,
    string ApiKey,
    long Timestamp,
    string Folder,
    string AllowedFormats,
    string Signature);

/// <summary>
/// Ký tham số upload để Admin FE gửi ảnh thẳng lên Cloudinary, không đi qua API.
/// Cloudinary chỉ nhận chữ ký có <c>timestamp</c> trong vòng 1 giờ.
/// </summary>
public sealed class CloudinaryUploadSigner(IOptions<CloudinaryOptions> options, TimeProvider timeProvider)
{
    public const string AllowedFormats = "jpg,jpeg,png,webp";

    /// <summary>Chữ ký cho một lần upload vào <paramref name="folder"/>.</summary>
    /// <exception cref="BusinessException"><c>UPLOAD_NOT_CONFIGURED</c> (503) khi chưa cấu hình Cloudinary.</exception>
    public UploadSignatureResponse CreateSignature(string folder)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw MediaErrors.UploadNotConfigured();
        }

        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var parameters = new Dictionary<string, string>
        {
            ["allowed_formats"] = AllowedFormats,
            ["folder"] = folder,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
        };

        return new UploadSignatureResponse(
            $"https://api.cloudinary.com/v1_1/{settings.CloudName}/image/upload",
            settings.ApiKey,
            timestamp,
            folder,
            AllowedFormats,
            Sign(parameters, settings.ApiSecret));
    }

    /// <summary>
    /// Thuật toán của Cloudinary: tham số sắp theo tên, nối <c>key=value</c> bằng <c>&amp;</c>, thêm api_secret
    /// ở cuối rồi băm SHA-1 (thuật toán mặc định của tài khoản Cloudinary).
    /// </summary>
    public static string Sign(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        var payload = string.Join(
            '&',
            parameters.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}={parameter.Value}"));

        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(payload + apiSecret)));
    }
}

public static class MediaErrors
{
    public const string UploadNotConfiguredCode = "UPLOAD_NOT_CONFIGURED";

    public static BusinessException UploadNotConfigured() => new(
        UploadNotConfiguredCode, "Chưa cấu hình dịch vụ lưu ảnh", StatusCodes.Status503ServiceUnavailable);
}

public static class MediaServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký <see cref="CloudinaryOptions"/> và <see cref="CloudinaryUploadSigner"/>. Nhiều module cùng gọi được,
    /// chỉ module đầu tiên đăng ký.
    /// </summary>
    public static IServiceCollection AddCloudinaryUploads(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(CloudinaryUploadSigner)))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddSingleton<CloudinaryUploadSigner>();
        return services;
    }
}

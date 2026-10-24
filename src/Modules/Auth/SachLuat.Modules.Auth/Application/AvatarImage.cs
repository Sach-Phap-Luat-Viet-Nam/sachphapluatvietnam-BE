using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SachLuat.SharedKernel.Media;

namespace SachLuat.Modules.Auth.Application;

/// <summary>Mỗi user đúng một ảnh avatar trên Cloudinary; upload mới ghi đè ảnh cũ ở cùng public_id.</summary>
internal static class AvatarImage
{
    /// <summary>
    /// <c>{AvatarFolder}/{userId}-{hmac}</c>. Đoạn HMAC (khóa là ApiSecret) để không đoán được link avatar của user khác
    /// từ id, và không ai tự tạo được chữ ký ghi vào ảnh của người khác.
    /// </summary>
    public static string PublicId(CloudinaryOptions options, long userId)
    {
        var id = userId.ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.ApiSecret), Encoding.UTF8.GetBytes(id));
        return $"{options.AvatarFolder}/{id}-{Convert.ToHexStringLower(hash.AsSpan(0, 8))}";
    }

    /// <summary>
    /// Chỉ nhận ảnh đúng public_id của user. Chưa cấu hình Cloudinary (local/test) thì nhận mọi link, giống ảnh bìa.
    /// </summary>
    public static bool AcceptsUrl(CloudinaryOptions options, long userId, string url) =>
        !options.IsConfigured || options.IsImageUrlOf(url, PublicId(options, userId));
}

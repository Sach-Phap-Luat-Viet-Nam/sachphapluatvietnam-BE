using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Content.Domain;

internal static class ContentErrors
{
    /// <summary>Cùng mã với Catalog để Admin FE gắn lỗi vào ô slug như các màn khác.</summary>
    public const string SlugDuplicateCode = "SLUG_DUPLICATE";

    public static BusinessException Invalid(string message) =>
        new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    public static BusinessException PopupNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy popup", StatusCodes.Status404NotFound);

    public static BusinessException StaticPageNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy trang", StatusCodes.Status404NotFound);

    public static BusinessException StaticPageSlugTaken(string slug) =>
        new(SlugDuplicateCode, $"Đường dẫn \"{slug}\" đã được trang khác sử dụng", StatusCodes.Status409Conflict);
}

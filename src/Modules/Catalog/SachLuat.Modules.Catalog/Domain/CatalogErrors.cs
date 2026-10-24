using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Catalog.Domain;

internal static class CatalogErrors
{
    public const string IsbnInvalidCode = "ISBN_INVALID";
    public const string IsbnDuplicateCode = "ISBN_DUPLICATE";
    public const string SlugInvalidCode = "SLUG_INVALID";
    public const string SkuCodeInvalidCode = "SKU_CODE_INVALID";
    public const string SkuCoverDuplicateCode = "SKU_COVER_DUPLICATE";
    public const string PriceInvalidCode = "PRICE_INVALID";
    public const string EditionNotFoundCode = "EDITION_NOT_FOUND";
    public const string CategoryParentInvalidCode = "CATEGORY_PARENT_INVALID";
    public const string PriceScheduleOverlapCode = "PRICE_SCHEDULE_OVERLAP";
    public const string PriceScheduleEndedCode = "PRICE_SCHEDULE_ENDED";

    public static BusinessException Required(string fieldLabel) =>
        new(ErrorCodes.ValidationError, $"Thiếu {fieldLabel}", StatusCodes.Status400BadRequest);

    public static BusinessException Invalid(string message) =>
        new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    public static BusinessException IsbnInvalid() =>
        new(IsbnInvalidCode, "ISBN không hợp lệ (13 chữ số, bắt đầu bằng 978 hoặc 979, đúng số kiểm tra)");

    public static BusinessException IsbnDuplicate() =>
        new(IsbnDuplicateCode, "ISBN đã tồn tại", StatusCodes.Status409Conflict);

    public static BusinessException SlugInvalid() =>
        new(SlugInvalidCode, "Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối");

    public static BusinessException SkuCodeInvalid() =>
        new(SkuCodeInvalidCode, "Mã SKU chỉ gồm chữ in hoa, số và dấu gạch nối, tối đa 50 ký tự");

    public static BusinessException SkuCoverDuplicate() =>
        new(SkuCoverDuplicateCode, "Ấn bản đã có SKU cho loại bìa này", StatusCodes.Status409Conflict);

    public static BusinessException PriceInvalid() =>
        new(PriceInvalidCode, "Giá phải là số nguyên VNĐ lớn hơn 0 và giá bán không vượt giá bìa");

    public static BusinessException PriceScheduleNotLower() =>
        new(PriceInvalidCode, "Giá theo lịch phải là số nguyên VNĐ và thấp hơn giá bán thường");

    public static BusinessException PriceScheduleOverlap() => new(
        PriceScheduleOverlapCode, "SKU đã có lịch giá khác trong khoảng thời gian này", StatusCodes.Status409Conflict);

    public static BusinessException PriceScheduleEnded() =>
        new(PriceScheduleEndedCode, "Lịch giá đã kết thúc, không sửa hoặc xóa được");

    public static BusinessException PhysicalInfoInvalid() =>
        new(ErrorCodes.ValidationError, "Số trang và trọng lượng phải lớn hơn 0", StatusCodes.Status400BadRequest);

    public static BusinessException EditionNotFound() =>
        new(EditionNotFoundCode, "Không tìm thấy ấn bản trong đầu sách", StatusCodes.Status404NotFound);

    public static BusinessException CategoryParentInvalid() =>
        new(CategoryParentInvalidCode, "Danh mục cha không hợp lệ");

    public const string SlugDuplicateCode = "SLUG_DUPLICATE";
    public const string SkuCodeDuplicateCode = "SKU_CODE_DUPLICATE";
    public const string CategoryHasChildrenCode = "CATEGORY_HAS_CHILDREN";
    public const string ReferenceNotFoundCode = "REFERENCE_NOT_FOUND";

    public static BusinessException NotFound(string label) =>
        new(ErrorCodes.NotFound, $"Không tìm thấy {label}", StatusCodes.Status404NotFound);

    /// <summary>Id tham chiếu trong request (danh mục, tác giả, văn bản) không tồn tại.</summary>
    public static BusinessException ReferenceNotFound(string label) =>
        new(ReferenceNotFoundCode, $"Có {label} không tồn tại");

    public static BusinessException SlugDuplicate() =>
        new(SlugDuplicateCode, "Slug đã được sử dụng", StatusCodes.Status409Conflict);

    public static BusinessException SkuCodeDuplicate() =>
        new(SkuCodeDuplicateCode, "Mã SKU đã tồn tại", StatusCodes.Status409Conflict);

    public static BusinessException CategoryHasChildren() => new(
        CategoryHasChildrenCode, "Không thể xóa danh mục còn danh mục con", StatusCodes.Status409Conflict);
}

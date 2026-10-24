using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Shipping.Domain;

internal static class ShippingErrors
{
    public const string AddressNotSupportedCode = "ADDRESS_NOT_SUPPORTED";
    public const string ProvinceDuplicateCode = "SHIPPING_PROVINCE_DUPLICATE";

    public static BusinessException Invalid(string message) =>
        new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    /// <summary>Mã lỗi và thông báo theo mục 16.2.</summary>
    public static BusinessException AddressNotSupported() =>
        new(AddressNotSupportedCode, "Chưa hỗ trợ giao đến địa chỉ này");

    public static BusinessException ProvinceDuplicate(string province) => new(
        ProvinceDuplicateCode,
        $"Tỉnh/thành \"{province}\" đã thuộc một vùng giao hàng khác",
        StatusCodes.Status409Conflict);

    public static BusinessException ZoneNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy vùng giao hàng", StatusCodes.Status404NotFound);
}

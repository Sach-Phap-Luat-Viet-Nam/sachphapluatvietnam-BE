using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Cart.Application;

internal static class CartErrors
{
    public const string MaxQuantityExceededCode = "MAX_QTY_EXCEEDED";
    public const string SkuNotAvailableCode = "SKU_NOT_AVAILABLE";
    public const string CartFullCode = "CART_FULL";
    public const string OutOfStockCode = "OUT_OF_STOCK";

    public static BusinessException MaxQuantityExceeded(int max) => new(
        MaxQuantityExceededCode, $"Tối đa {max} cuốn mỗi loại, vui lòng gửi yêu cầu báo giá sỉ");

    public static BusinessException SkuNotAvailable() =>
        new(SkuNotAvailableCode, "Sách hiện không bán hoặc không tồn tại");

    public static BusinessException CartFull(int max) => new(CartFullCode, $"Giỏ hàng tối đa {max} loại sách");

    public static BusinessException OutOfStock() => new(
        OutOfStockCode, "Sách đã hết hàng hoặc không đủ số lượng", StatusCodes.Status409Conflict);
}

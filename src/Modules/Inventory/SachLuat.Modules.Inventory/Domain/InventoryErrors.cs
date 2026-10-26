using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Inventory.Domain;

internal static class InventoryErrors
{
    public const string OutOfStockCode = "OUT_OF_STOCK";
    public const string ReceiptNotDraftCode = "RECEIPT_NOT_DRAFT";
    public const string ReceiptForbiddenCode = "RECEIPT_FORBIDDEN";
    public const string BatchCodeDuplicateCode = "BATCH_CODE_DUPLICATE";
    public const string ReceiptEmptyCode = "RECEIPT_EMPTY";

    public static BusinessException OutOfStock() => new(
        OutOfStockCode, "Sách đã hết hàng hoặc không đủ số lượng", StatusCodes.Status409Conflict);

    public static BusinessException ReceiptNotDraft() => new(
        ReceiptNotDraftCode, "Phiếu nhập đã được duyệt hoặc từ chối", StatusCodes.Status409Conflict);

    public static BusinessException ReceiptForbidden() => new(
        ReceiptForbiddenCode, "Chỉ người lập phiếu hoặc Super Admin được sửa, xóa phiếu nháp", StatusCodes.Status403Forbidden);

    public static BusinessException BatchCodeDuplicate(string batchCode) => new(
        BatchCodeDuplicateCode, $"Mã lô {batchCode} đã tồn tại cho SKU này", StatusCodes.Status409Conflict);

    public static BusinessException ReceiptEmpty() => new(ReceiptEmptyCode, "Phiếu nhập phải có ít nhất một dòng");

    public static BusinessException NotFound(string label) =>
        new(ErrorCodes.NotFound, $"Không tìm thấy {label}", StatusCodes.Status404NotFound);

    public static BusinessException SkuNotFound() =>
        new("REFERENCE_NOT_FOUND", "Có SKU không tồn tại");
}

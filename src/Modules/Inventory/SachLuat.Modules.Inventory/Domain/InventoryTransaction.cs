using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Inventory.Domain;

/// <summary>Dòng thẻ kho, chỉ thêm không sửa (không xóa giao dịch kho, mục 10.3).</summary>
internal sealed class InventoryTransaction : AuditableEntity
{
    public InventoryTransaction(
        long skuId,
        long? printBatchId,
        TransactionType type,
        int quantity,
        string reference,
        long? createdBy,
        string? note = null)
    {
        SkuId = skuId;
        PrintBatchId = printBatchId;
        Type = type;
        Quantity = quantity;
        Reference = reference;
        CreatedBy = createdBy;
        Note = note;
    }

    // EF Core
    private InventoryTransaction()
    {
    }

    public long SkuId { get; private set; }

    public long? PrintBatchId { get; private set; }

    public TransactionType Type { get; private set; }

    /// <summary>Dương: nhập; âm: xuất.</summary>
    public int Quantity { get; private set; }

    /// <summary>Mã đơn, mã phiếu nhập (<c>GR-{id}</c>) hoặc mã phiếu xuất.</summary>
    public string Reference { get; private set; } = string.Empty;

    public long? CreatedBy { get; private set; }

    public string? Note { get; private set; }
}

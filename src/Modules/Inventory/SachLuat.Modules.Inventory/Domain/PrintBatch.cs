using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Inventory.Domain;

/// <summary>Lô in đã nhập kho; <see cref="Remaining"/> giảm dần khi xuất theo FIFO (BR-13).</summary>
internal sealed class PrintBatch : AuditableEntity
{
    public PrintBatch(
        long skuId,
        string batchCode,
        string? printerName,
        int quantity,
        decimal unitCost,
        DateOnly receivedAt)
    {
        SkuId = skuId;
        BatchCode = batchCode;
        PrinterName = printerName;
        Quantity = quantity;
        UnitCost = unitCost;
        ReceivedAt = receivedAt;
        Remaining = quantity;
    }

    // EF Core
    private PrintBatch()
    {
    }

    public long SkuId { get; private set; }

    public string BatchCode { get; private set; } = string.Empty;

    public string? PrinterName { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Giá vốn mỗi cuốn (VNĐ).</summary>
    public decimal UnitCost { get; private set; }

    public DateOnly ReceivedAt { get; private set; }

    public int Remaining { get; private set; }

    /// <summary>Lấy tối đa <paramref name="requested"/> cuốn, trả số thực lấy được.</summary>
    public int Take(int requested)
    {
        var taken = Math.Min(requested, Remaining);
        Remaining -= taken;
        return taken;
    }

    public void PutBack(int quantity) => Remaining = Math.Min(Quantity, Remaining + quantity);
}

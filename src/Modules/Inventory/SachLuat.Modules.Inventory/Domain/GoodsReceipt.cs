using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Inventory.Domain;

/// <summary>
/// Phiếu nhập kho theo lô in (US-05): lập ở trạng thái nháp, người khác duyệt thì tồn mới tăng.
/// </summary>
internal sealed class GoodsReceipt : AuditableEntity
{
    private readonly List<GoodsReceiptLine> _lines = [];

    public GoodsReceipt(long createdBy, string? note, IEnumerable<GoodsReceiptLine> lines)
    {
        CreatedBy = createdBy;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        _lines.AddRange(lines);
        if (_lines.Count == 0)
        {
            throw InventoryErrors.ReceiptEmpty();
        }

        var duplicate = _lines
            .GroupBy(line => (line.SkuId, line.BatchCode))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw InventoryErrors.BatchCodeDuplicate(duplicate.Key.BatchCode);
        }

        Status = ReceiptStatus.Draft;
    }

    // EF Core
    private GoodsReceipt()
    {
    }

    public ReceiptStatus Status { get; private set; }

    public string? Note { get; private set; }

    public long CreatedBy { get; private set; }

    public long? DecidedBy { get; private set; }

    public DateTime? DecidedAt { get; private set; }

    public string? RejectReason { get; private set; }

    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines;

    public void Approve(long approverId, DateTime now)
    {
        EnsureCanDecide(approverId);
        Status = ReceiptStatus.Approved;
        DecidedBy = approverId;
        DecidedAt = now;
    }

    public void Reject(long approverId, string reason, DateTime now)
    {
        EnsureCanDecide(approverId);
        Status = ReceiptStatus.Rejected;
        DecidedBy = approverId;
        DecidedAt = now;
        RejectReason = reason.Trim();
    }

    private void EnsureCanDecide(long approverId)
    {
        if (Status != ReceiptStatus.Draft)
        {
            throw InventoryErrors.ReceiptNotDraft();
        }

        // Tách người lập và người duyệt để phiếu nhập có kiểm soát chéo.
        if (approverId == CreatedBy)
        {
            throw InventoryErrors.ReceiptSelfApproval();
        }
    }
}

internal sealed class GoodsReceiptLine
{
    public GoodsReceiptLine(
        long skuId,
        string batchCode,
        string? printerName,
        int quantity,
        decimal unitCost,
        DateOnly receivedAt)
    {
        SkuId = skuId;
        BatchCode = batchCode.Trim().ToUpperInvariant();
        PrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();
        Quantity = quantity;
        UnitCost = unitCost;
        ReceivedAt = receivedAt;
    }

    // EF Core
    private GoodsReceiptLine()
    {
    }

    public long Id { get; private set; }

    public long GoodsReceiptId { get; private set; }

    public long SkuId { get; private set; }

    public string BatchCode { get; private set; } = string.Empty;

    public string? PrinterName { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public DateOnly ReceivedAt { get; private set; }

    /// <summary>Lô in tạo ra khi phiếu được duyệt.</summary>
    public long? PrintBatchId { get; private set; }

    public void LinkBatch(long printBatchId) => PrintBatchId = printBatchId;
}

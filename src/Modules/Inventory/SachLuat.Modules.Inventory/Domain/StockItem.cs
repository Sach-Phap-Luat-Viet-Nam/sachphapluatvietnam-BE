using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Inventory.Domain;

/// <summary>
/// Tồn kho của một SKU. Bất biến: <c>Available + Reserved</c> = tổng <c>Remaining</c> các lô in của SKU.
/// Cộng/trừ tồn đi qua câu UPDATE có điều kiện trong <c>InventoryService</c> để không bán âm khi mua đồng thời.
/// </summary>
internal sealed class StockItem : IAuditable
{
    public const int DefaultLowStockThreshold = 50;

    public StockItem(long skuId)
    {
        SkuId = skuId;
        LowStockThreshold = DefaultLowStockThreshold;
    }

    public long SkuId { get; private set; }

    /// <summary>Tồn khả dụng (bán được).</summary>
    public int Available { get; private set; }

    /// <summary>Đang tạm giữ cho đơn chưa đóng gói (BR-11).</summary>
    public int Reserved { get; private set; }

    /// <summary>Ngưỡng cảnh báo tồn thấp (BR-50), mặc định 50.</summary>
    public int LowStockThreshold { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public void SetLowStockThreshold(int threshold) => LowStockThreshold = threshold;
}

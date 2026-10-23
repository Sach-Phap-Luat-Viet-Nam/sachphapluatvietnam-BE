namespace SachLuat.Modules.Inventory.Contracts;

/// <summary>
/// Tồn kho cho module khác. Mọi thao tác theo <c>reference</c> (mã đơn) đều idempotent: gọi lại không ghi trùng.
/// Vi phạm nghiệp vụ ném <c>BusinessException</c> (ví dụ <c>OUT_OF_STOCK</c>).
/// </summary>
public interface IInventoryService
{
    /// <summary>Số lượng khả dụng (chưa tạm giữ) theo SKU; SKU chưa từng nhập kho trả 0.</summary>
    Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<long> skuIds,
        CancellationToken cancellationToken);

    /// <summary>BR-11: tạm giữ tồn khi tạo đơn. Thiếu hàng bất kỳ SKU nào thì không giữ gì.</summary>
    Task ReserveAsync(string reference, IReadOnlyCollection<StockLine> lines, CancellationToken cancellationToken);

    /// <summary>Hoàn tồn tạm giữ khi hủy đơn chưa đóng gói.</summary>
    Task ReleaseAsync(string reference, CancellationToken cancellationToken);

    /// <summary>
    /// BR-11, BR-13: trừ tồn thật khi đóng gói, xuất theo FIFO lô in.
    /// Trả giá vốn bình quân mỗi cuốn theo SKU (lưu vào <c>order_items.cost_price</c>).
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> DeductAsync(string reference, CancellationToken cancellationToken);

    /// <summary>Nhập lại kho hàng hoàn về (đã trừ trước đó), trả về đúng các lô đã xuất.</summary>
    Task RestockAsync(string reference, CancellationToken cancellationToken);
}

public sealed record StockLine(long SkuId, int Quantity);

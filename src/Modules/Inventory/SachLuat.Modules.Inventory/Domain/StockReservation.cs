using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Inventory.Domain;

/// <summary>Tạm giữ tồn cho một đơn (BR-11); vòng đời: active → released | deducted → returned.</summary>
internal sealed class StockReservation : AuditableEntity
{
    public StockReservation(string reference, long skuId, int quantity)
    {
        Reference = reference;
        SkuId = skuId;
        Quantity = quantity;
        Status = ReservationStatus.Active;
    }

    // EF Core
    private StockReservation()
    {
    }

    public string Reference { get; private set; } = string.Empty;

    public long SkuId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public void MarkReleased() => Status = ReservationStatus.Released;

    public void MarkDeducted() => Status = ReservationStatus.Deducted;

    public void MarkReturned() => Status = ReservationStatus.Returned;
}

using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Editions;

/// <summary>
/// Giá bán có thời hạn của một SKU (lịch giá, FR-63). Trong khung <c>[StartsAt, EndsAt)</c> SKU bán theo giá này
/// nếu thấp hơn giá bán thường. Thuộc aggregate <see cref="Sku"/>, tạo/sửa qua các method của SKU.
/// </summary>
internal sealed class SkuPriceSchedule : AuditableEntity
{
    public const int NoteMaxLength = 255;

    internal SkuPriceSchedule(decimal salePrice, DateTime startsAt, DateTime endsAt, string? note) =>
        Update(salePrice, startsAt, endsAt, note);

    // EF Core
    private SkuPriceSchedule()
    {
    }

    public long SkuId { get; private set; }

    /// <summary>Giá bán trong thời gian áp dụng (VNĐ).</summary>
    public decimal SalePrice { get; private set; }

    /// <summary>UTC, tính từ thời điểm này.</summary>
    public DateTime StartsAt { get; private set; }

    /// <summary>UTC, hết hiệu lực từ thời điểm này.</summary>
    public DateTime EndsAt { get; private set; }

    /// <summary>Ghi chú nội bộ, ví dụ tên chương trình "Sale mùa thi".</summary>
    public string? Note { get; private set; }

    public bool IsActiveAt(DateTime now) => StartsAt <= now && now < EndsAt;

    public bool HasEndedAt(DateTime now) => EndsAt <= now;

    public bool Overlaps(DateTime startsAt, DateTime endsAt) => StartsAt < endsAt && startsAt < EndsAt;

    public PriceScheduleStatus StatusAt(DateTime now) =>
        HasEndedAt(now) ? PriceScheduleStatus.Ended
        : IsActiveAt(now) ? PriceScheduleStatus.Running
        : PriceScheduleStatus.Scheduled;

    internal void Update(decimal salePrice, DateTime startsAt, DateTime endsAt, string? note)
    {
        if (endsAt <= startsAt)
        {
            throw CatalogErrors.Invalid("Thời gian kết thúc phải sau thời gian bắt đầu");
        }

        SalePrice = salePrice;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Note = Guard.Optional(note);
    }
}

internal enum PriceScheduleStatus
{
    Scheduled,
    Running,
    Ended,
}

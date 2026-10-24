using System.Text.RegularExpressions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Editions;

/// <summary>
/// Mã hàng (bìa mềm/cứng) của một ấn bản. Tồn kho và lô in thuộc module Inventory, tham chiếu qua <c>Id</c>.
/// </summary>
internal sealed partial class Sku : AuditableEntity
{
    public const int SkuCodeMaxLength = 50;

    // numeric(12,0): tối đa 999.999.999.999 VNĐ.
    private const decimal MaxPrice = 999_999_999_999m;

    private readonly List<SkuPriceSchedule> _priceSchedules = [];

    internal Sku(string skuCode, CoverType coverType, decimal listPrice, decimal salePrice)
    {
        SkuCode = NormalizeSkuCode(skuCode);
        CoverType = coverType;
        ChangePrices(listPrice, salePrice);
    }

    // EF Core
    private Sku()
    {
    }

    public long EditionId { get; private set; }

    public string SkuCode { get; private set; } = string.Empty;

    public CoverType CoverType { get; private set; }

    /// <summary>Giá bìa (VNĐ).</summary>
    public decimal ListPrice { get; private set; }

    /// <summary>Giá bán (VNĐ), không vượt giá bìa.</summary>
    public decimal SalePrice { get; private set; }

    public bool AllowPreorder { get; private set; }

    /// <summary>Lịch giá; chỉ chứa các dòng đã được nạp (Include) cùng SKU.</summary>
    public IReadOnlyCollection<SkuPriceSchedule> PriceSchedules => _priceSchedules;

    public void ChangePrices(decimal listPrice, decimal salePrice)
    {
        if (!IsWholeVnd(listPrice) || !IsWholeVnd(salePrice) || salePrice > listPrice)
        {
            throw CatalogErrors.PriceInvalid();
        }

        ListPrice = listPrice;
        SalePrice = salePrice;
    }

    /// <summary>
    /// Lịch giá đang chạy tại <paramref name="now"/> và thấp hơn giá bán thường; null thì bán theo
    /// <see cref="SalePrice"/>. Cần nạp <see cref="PriceSchedules"/>.
    /// </summary>
    /// <remarks>
    /// So với giá bán thường ở đây (không chỉ lúc tạo lịch) vì giá thường có thể bị hạ sau đó: giá hiện hành luôn
    /// là giá thấp hơn, nên không bao giờ vượt giá bìa.
    /// </remarks>
    public SkuPriceSchedule? ActivePriceScheduleAt(DateTime now) =>
        _priceSchedules.FirstOrDefault(schedule => schedule.IsActiveAt(now) && schedule.SalePrice < SalePrice);

    /// <summary>Giá khách trả tại <paramref name="now"/>: giá lịch nếu thấp hơn, không thì giá bán thường.</summary>
    public decimal CurrentPriceAt(DateTime now) => ActivePriceScheduleAt(now)?.SalePrice ?? SalePrice;

    /// <remarks>Cần nạp mọi lịch giá của SKU để kiểm tra chồng lịch.</remarks>
    public SkuPriceSchedule AddPriceSchedule(
        decimal salePrice,
        DateTime startsAt,
        DateTime endsAt,
        string? note,
        DateTime now)
    {
        EnsureValidSchedule(null, salePrice, startsAt, endsAt, now);
        var schedule = new SkuPriceSchedule(salePrice, startsAt, endsAt, note);
        _priceSchedules.Add(schedule);
        return schedule;
    }

    /// <summary>Sửa lịch chưa kết thúc; lịch đã kết thúc giữ nguyên làm lịch sử giá.</summary>
    public SkuPriceSchedule UpdatePriceSchedule(
        long scheduleId,
        decimal salePrice,
        DateTime startsAt,
        DateTime endsAt,
        string? note,
        DateTime now)
    {
        var schedule = FindOpenSchedule(scheduleId, now);
        EnsureValidSchedule(schedule, salePrice, startsAt, endsAt, now);
        schedule.Update(salePrice, startsAt, endsAt, note);
        return schedule;
    }

    /// <summary>Xóa lịch chưa kết thúc (lịch đang chạy thì dừng giảm giá ngay).</summary>
    public void RemovePriceSchedule(long scheduleId, DateTime now) =>
        _priceSchedules.Remove(FindOpenSchedule(scheduleId, now));

    public void SetPreorder(bool allowPreorder) => AllowPreorder = allowPreorder;

    private SkuPriceSchedule FindOpenSchedule(long scheduleId, DateTime now)
    {
        var schedule = _priceSchedules.SingleOrDefault(item => item.Id == scheduleId)
            ?? throw CatalogErrors.NotFound("lịch giá");
        return schedule.HasEndedAt(now) ? throw CatalogErrors.PriceScheduleEnded() : schedule;
    }

    private void EnsureValidSchedule(
        SkuPriceSchedule? current,
        decimal salePrice,
        DateTime startsAt,
        DateTime endsAt,
        DateTime now)
    {
        if (!IsWholeVnd(salePrice))
        {
            throw CatalogErrors.PriceInvalid();
        }

        if (salePrice >= SalePrice)
        {
            throw CatalogErrors.PriceScheduleNotLower();
        }

        if (endsAt <= startsAt)
        {
            throw CatalogErrors.Invalid("Thời gian kết thúc phải sau thời gian bắt đầu");
        }

        if (endsAt <= now)
        {
            throw CatalogErrors.Invalid("Thời gian kết thúc phải ở tương lai");
        }

        // Mỗi thời điểm tối đa một lịch giá, để giá hiện hành luôn rõ ràng.
        if (_priceSchedules.Any(other => !ReferenceEquals(other, current) && other.Overlaps(startsAt, endsAt)))
        {
            throw CatalogErrors.PriceScheduleOverlap();
        }
    }

    private static bool IsWholeVnd(decimal price) => price is > 0 and <= MaxPrice && decimal.Truncate(price) == price;

    private static string NormalizeSkuCode(string? skuCode)
    {
        var normalized = skuCode?.Trim().ToUpperInvariant();
        return normalized is { Length: > 0 and <= SkuCodeMaxLength } && SkuCodePattern().IsMatch(normalized)
            ? normalized
            : throw CatalogErrors.SkuCodeInvalid();
    }

    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$")]
    private static partial Regex SkuCodePattern();
}

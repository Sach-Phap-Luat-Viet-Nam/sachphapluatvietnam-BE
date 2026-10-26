using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Promotion.Domain;

/// <summary>
/// Combo bộ sách cố định (FR-16): mua đủ các SKU trong bộ thì được giá combo thay cho tổng giá lẻ.
/// Combo không có tồn kho hay SKU riêng, chỉ là ưu đãi giá trên các SKU có sẵn nên không nằm ở Catalog.
/// </summary>
internal sealed class Combo : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 255;
    public const int DescriptionMaxLength = 1000;
    public const int ImageUrlMaxLength = 500;
    public const int MinItems = 2;
    public const int MaxItems = 10;
    public const int MaxItemQuantity = 20;

    private readonly List<ComboItem> _items = [];

    public Combo(ComboDetails details, IEnumerable<ComboItemSpec> items)
    {
        Update(details);
        SetItems(items);
    }

    // EF Core
    private Combo()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Đường dẫn trang <c>/combo/{slug}</c>; duy nhất giữa các combo chưa xóa.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? ImageUrl { get; private set; }

    /// <summary>Giá khách trả cho một bộ (VNĐ).</summary>
    public decimal ComboPrice { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime StartsAt { get; private set; }

    /// <summary>null: chạy đến khi tắt.</summary>
    public DateTime? EndsAt { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyCollection<ComboItem> Items => _items;

    /// <summary>Đang bật và trong lịch chạy tại <paramref name="now"/> (UTC); dùng được trong truy vấn EF.</summary>
    public static Expression<Func<Combo, bool>> RunningAt(DateTime now) =>
        combo => combo.IsActive && combo.StartsAt <= now && (combo.EndsAt == null || combo.EndsAt > now);

    /// <summary>Cùng điều kiện với <see cref="RunningAt"/> nhưng cho một thực thể đã nạp.</summary>
    public bool IsRunningAt(DateTime now) =>
        IsActive && StartsAt <= now && (EndsAt == null || EndsAt > now);

    public PromotionStatus StatusAt(DateTime now)
    {
        if (!IsActive)
        {
            return PromotionStatus.Inactive;
        }

        if (now < StartsAt)
        {
            return PromotionStatus.Scheduled;
        }

        return EndsAt <= now ? PromotionStatus.Ended : PromotionStatus.Running;
    }

    public void Update(ComboDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name))
        {
            throw PromotionErrors.Invalid("Thiếu tên combo");
        }

        if (!ComboSlug.IsValid(details.Slug))
        {
            throw PromotionErrors.Invalid("Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối");
        }

        if (details.ComboPrice is <= 0 or > PromotionCampaign.MaxMoney
            || details.ComboPrice != decimal.Floor(details.ComboPrice))
        {
            throw PromotionErrors.Invalid("Giá combo phải là số nguyên VNĐ lớn hơn 0");
        }

        if (details.EndsAt is { } endsAt && endsAt <= details.StartsAt)
        {
            throw PromotionErrors.Invalid("Thời gian kết thúc phải sau thời gian bắt đầu");
        }

        Name = details.Name.Trim();
        Slug = details.Slug;
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(details.ImageUrl) ? null : details.ImageUrl.Trim();
        ComboPrice = details.ComboPrice;
        StartsAt = details.StartsAt;
        EndsAt = details.EndsAt;
        IsActive = details.IsActive;
    }

    /// <summary>
    /// Thay danh sách sách trong bộ. Không kiểm tra SKU có tồn tại vì Catalog là module khác (không khóa ngoại
    /// chéo schema); service tầng ứng dụng kiểm tra qua <c>ICatalogSkuReader</c>.
    /// </summary>
    public void SetItems(IEnumerable<ComboItemSpec> items)
    {
        var specs = items.ToList();
        if (specs.Count is < MinItems or > MaxItems)
        {
            throw PromotionErrors.Invalid($"Combo gồm từ {MinItems} đến {MaxItems} sách khác nhau");
        }

        if (specs.Select(spec => spec.SkuId).Distinct().Count() != specs.Count)
        {
            throw PromotionErrors.Invalid("Một SKU chỉ xuất hiện một lần trong combo, tăng số lượng thay vì thêm dòng");
        }

        if (specs.Any(spec => spec.SkuId <= 0 || spec.Quantity is < 1 or > MaxItemQuantity))
        {
            throw PromotionErrors.Invalid($"SKU không hợp lệ hoặc số lượng mỗi sách phải từ 1 đến {MaxItemQuantity}");
        }

        _items.RemoveAll(item => specs.All(spec => spec.SkuId != item.SkuId));
        foreach (var spec in specs)
        {
            var existing = _items.SingleOrDefault(item => item.SkuId == spec.SkuId);
            if (existing is null)
            {
                _items.Add(new ComboItem(spec.SkuId, spec.Quantity));
            }
            else
            {
                existing.SetQuantity(spec.Quantity);
            }
        }
    }
}

internal sealed class ComboItem(long skuId, int quantity)
{
    public long Id { get; private set; }

    public long ComboId { get; private set; }

    public long SkuId { get; private set; } = skuId;

    /// <summary>Số cuốn của SKU này trong một bộ.</summary>
    public int Quantity { get; private set; } = quantity;

    public void SetQuantity(int quantity) => Quantity = quantity;
}

internal static partial class ComboSlug
{
    public const int MaxLength = 255;

    /// <summary>Chữ thường không dấu, số, nối bằng một dấu gạch: <c>combo-on-thi-luat-su</c>.</summary>
    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}

/// <param name="StartsAt">UTC.</param>
/// <param name="EndsAt">UTC.</param>
internal sealed record ComboDetails(
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    decimal ComboPrice,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive);

internal sealed record ComboItemSpec(long SkuId, int Quantity);

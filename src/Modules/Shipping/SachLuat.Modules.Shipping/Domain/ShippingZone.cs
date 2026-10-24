using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Shipping.Domain;

/// <summary>
/// Vùng giao hàng: nhóm tỉnh/thành dùng chung một biểu phí theo trọng lượng và thời gian giao dự kiến (FR-34).
/// Mỗi tỉnh chỉ thuộc một vùng.
/// </summary>
internal sealed class ShippingZone : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int MaxWeightGram = 1_000_000;
    public const decimal MaxFee = 10_000_000m;
    public const int MaxEtaDays = 60;

    private readonly List<ShippingZoneProvince> _provinces = [];

    public ShippingZone(ZoneDetails details) => Update(details);

    // EF Core
    private ShippingZone()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Phí <see cref="BaseFee"/> áp cho đơn đến hết số gram này.</summary>
    public int BaseWeightGram { get; private set; }

    public decimal BaseFee { get; private set; }

    /// <summary>Mỗi bước (làm tròn lên) vượt <see cref="BaseWeightGram"/> cộng thêm <see cref="StepFee"/>.</summary>
    public int StepWeightGram { get; private set; }

    public decimal StepFee { get; private set; }

    public int EtaMinDays { get; private set; }

    public int EtaMaxDays { get; private set; }

    public IReadOnlyCollection<ShippingZoneProvince> Provinces => _provinces;

    public void Update(ZoneDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name))
        {
            throw ShippingErrors.Invalid("Thiếu tên vùng giao hàng");
        }

        if (details.BaseWeightGram is <= 0 or > MaxWeightGram || details.StepWeightGram is <= 0 or > MaxWeightGram)
        {
            throw ShippingErrors.Invalid("Mốc trọng lượng phải lớn hơn 0");
        }

        if (details.BaseFee is < 0 or > MaxFee || details.StepFee is < 0 or > MaxFee)
        {
            throw ShippingErrors.Invalid("Phí vận chuyển không hợp lệ");
        }

        if (details.EtaMinDays < 0 || details.EtaMaxDays < details.EtaMinDays || details.EtaMaxDays > MaxEtaDays)
        {
            throw ShippingErrors.Invalid("Thời gian giao dự kiến không hợp lệ");
        }

        Name = details.Name.Trim();
        IsActive = details.IsActive;
        SortOrder = details.SortOrder;
        BaseWeightGram = details.BaseWeightGram;
        BaseFee = details.BaseFee;
        StepWeightGram = details.StepWeightGram;
        StepFee = details.StepFee;
        EtaMinDays = details.EtaMinDays;
        EtaMaxDays = details.EtaMaxDays;
    }

    /// <summary>Thay danh sách tỉnh/thành, chỉ thêm/xóa phần chênh để không đụng index duy nhất khi lưu.</summary>
    public void SetProvinces(IEnumerable<string> names)
    {
        var wanted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var key = ProvinceKey.Normalize(name);
            if (key.Length == 0 || key.Length > ProvinceKey.MaxLength)
            {
                throw ShippingErrors.Invalid($"Tên tỉnh/thành không hợp lệ: \"{name}\"");
            }

            wanted.TryAdd(key, name.Trim());
        }

        _provinces.RemoveAll(province => !wanted.ContainsKey(province.ProvinceKey));
        var existing = _provinces.Select(province => province.ProvinceKey).ToHashSet(StringComparer.Ordinal);
        _provinces.AddRange(wanted
            .Where(pair => !existing.Contains(pair.Key))
            .Select(pair => new ShippingZoneProvince(pair.Key, pair.Value)));
    }

    /// <summary>Phí theo biểu giá cho đơn nặng <paramref name="weightGram"/> gram.</summary>
    public decimal CalculateFee(int weightGram)
    {
        if (weightGram <= BaseWeightGram)
        {
            return BaseFee;
        }

        var extraSteps = (weightGram - BaseWeightGram + StepWeightGram - 1) / StepWeightGram;
        return BaseFee + (extraSteps * StepFee);
    }
}

internal sealed class ShippingZoneProvince(string provinceKey, string name)
{
    public long Id { get; private set; }

    public long ZoneId { get; private set; }

    /// <summary>Kết quả <see cref="Domain.ProvinceKey.Normalize"/>; duy nhất trên toàn hệ thống.</summary>
    public string ProvinceKey { get; private set; } = provinceKey;

    /// <summary>Tên hiển thị trên Admin.</summary>
    public string Name { get; private set; } = name;
}

internal sealed record ZoneDetails(
    string Name,
    bool IsActive,
    int SortOrder,
    int BaseWeightGram,
    decimal BaseFee,
    int StepWeightGram,
    decimal StepFee,
    int EtaMinDays,
    int EtaMaxDays);

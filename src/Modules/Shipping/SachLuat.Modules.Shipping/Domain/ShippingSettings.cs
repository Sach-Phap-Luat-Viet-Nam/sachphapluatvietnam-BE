using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Shipping.Domain;

/// <summary>Cấu hình vận chuyển dùng chung (FR-99); đúng một dòng, <c>id = 1</c>.</summary>
internal sealed class ShippingSettings : AuditableEntity
{
    public const long SingletonId = 1;
    public const int MaxWeightGram = 100_000;
    public const decimal MaxMoney = 1_000_000_000m;

    public ShippingSettings(ShippingSettingsValues values)
    {
        Id = SingletonId;
        Update(values);
    }

    // EF Core
    private ShippingSettings()
    {
    }

    /// <summary>BR-14: miễn phí ship từ mức tạm tính này (trước giảm giá); null là không miễn phí theo ngưỡng.</summary>
    public decimal? FreeShippingThreshold { get; private set; }

    /// <summary>Trọng lượng bao bì cộng vào mỗi đơn (mục 12.2).</summary>
    public int PackagingWeightGram { get; private set; }

    /// <summary>Dùng khi ấn bản chưa nhập trọng lượng.</summary>
    public int DefaultBookWeightGram { get; private set; }

    /// <summary>Phí cố định khi chưa chọn tỉnh hoặc khi chưa cấu hình vùng nào.</summary>
    public decimal FlatFee { get; private set; }

    public ShippingSettingsValues Values => new(
        FreeShippingThreshold, PackagingWeightGram, DefaultBookWeightGram, FlatFee);

    public void Update(ShippingSettingsValues values)
    {
        if (values.FreeShippingThreshold is < 0 or > MaxMoney || values.FlatFee is < 0 or > MaxMoney)
        {
            throw ShippingErrors.Invalid("Số tiền không hợp lệ");
        }

        if (values.PackagingWeightGram is < 0 or > MaxWeightGram
            || values.DefaultBookWeightGram is <= 0 or > MaxWeightGram)
        {
            throw ShippingErrors.Invalid("Trọng lượng không hợp lệ");
        }

        FreeShippingThreshold = values.FreeShippingThreshold;
        PackagingWeightGram = values.PackagingWeightGram;
        DefaultBookWeightGram = values.DefaultBookWeightGram;
        FlatFee = values.FlatFee;
    }
}

internal sealed record ShippingSettingsValues(
    decimal? FreeShippingThreshold,
    int PackagingWeightGram,
    int DefaultBookWeightGram,
    decimal FlatFee);

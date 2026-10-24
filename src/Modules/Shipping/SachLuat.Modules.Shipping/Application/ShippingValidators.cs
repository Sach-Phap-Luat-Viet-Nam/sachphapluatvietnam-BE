using FluentValidation;
using SachLuat.Modules.Shipping.Domain;

namespace SachLuat.Modules.Shipping.Application;

internal sealed class UpsertZoneRequestValidator : AbstractValidator<UpsertZoneRequest>
{
    public const int MaxProvinces = 63;

    public UpsertZoneRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(ShippingZone.NameMaxLength);
        RuleFor(request => request.SortOrder).InclusiveBetween(0, 10_000);
        RuleFor(request => request.Provinces).NotNull().Must(provinces => provinces is { Count: > 0 and <= MaxProvinces })
            .WithMessage($"Vùng phải có từ 1 đến {MaxProvinces} tỉnh/thành");
        RuleForEach(request => request.Provinces).NotEmpty().MaximumLength(ProvinceKey.MaxLength);
        RuleFor(request => request.Provinces)
            .Must(provinces => provinces.Select(ProvinceKey.Normalize).Distinct().Count() == provinces.Count)
            .When(request => request.Provinces is { Count: > 0 })
            .WithMessage("Danh sách tỉnh/thành bị trùng");
        RuleFor(request => request.BaseWeightGram).InclusiveBetween(1, ShippingZone.MaxWeightGram);
        RuleFor(request => request.StepWeightGram).InclusiveBetween(1, ShippingZone.MaxWeightGram);
        RuleFor(request => request.BaseFee).InclusiveBetween(0, ShippingZone.MaxFee).PrecisionScale(12, 0, true);
        RuleFor(request => request.StepFee).InclusiveBetween(0, ShippingZone.MaxFee).PrecisionScale(12, 0, true);
        RuleFor(request => request.EtaMinDays).InclusiveBetween(0, ShippingZone.MaxEtaDays);
        RuleFor(request => request.EtaMaxDays).InclusiveBetween(0, ShippingZone.MaxEtaDays)
            .GreaterThanOrEqualTo(request => request.EtaMinDays)
            .WithMessage("Số ngày giao tối đa phải lớn hơn hoặc bằng số ngày tối thiểu");
    }
}

internal sealed class ShippingSettingsRequestValidator : AbstractValidator<ShippingSettingsRequest>
{
    public ShippingSettingsRequestValidator()
    {
        RuleFor(request => request.FreeShippingThreshold)
            .InclusiveBetween(0, ShippingSettings.MaxMoney).PrecisionScale(12, 0, true)
            .When(request => request.FreeShippingThreshold is not null);
        RuleFor(request => request.FlatFee)
            .InclusiveBetween(0, ShippingSettings.MaxMoney).PrecisionScale(12, 0, true);
        RuleFor(request => request.PackagingWeightGram).InclusiveBetween(0, ShippingSettings.MaxWeightGram);
        RuleFor(request => request.DefaultBookWeightGram).InclusiveBetween(1, ShippingSettings.MaxWeightGram);
    }
}

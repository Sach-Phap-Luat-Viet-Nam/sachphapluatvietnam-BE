using FluentValidation;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Promotion.Application;

internal sealed class UpsertPromotionRequestValidator : AbstractValidator<UpsertPromotionRequest>
{
    public const int MaxPriority = 1000;

    public UpsertPromotionRequestValidator(IOptions<PromotionOptions> options)
    {
        var maxPercent = options.Value.MaxDiscountPercent;

        RuleFor(request => request.Name).NotEmpty().MaximumLength(PromotionCampaign.NameMaxLength);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(PromotionCampaign.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(PromotionCampaign.DescriptionMaxLength);
        RuleFor(request => request.Kind).IsInEnum().NotEqual(PromotionKind.Combo)
            .WithMessage("Combo được quản lý ở mục combo, không tạo như chương trình khuyến mãi");
        RuleFor(request => request.Mode).IsInEnum();
        RuleFor(request => request.DiscountType).IsInEnum();
        RuleFor(request => request.ScopeType).IsInEnum();
        RuleFor(request => request.Audience).IsInEnum();

        RuleFor(request => request.DiscountValue)
            .InclusiveBetween(1, 100)
            .When(request => request.DiscountType == DiscountType.Percent)
            .WithMessage("Mức giảm theo phần trăm phải từ 1 đến 100");
        RuleFor(request => request.DiscountValue)
            .InclusiveBetween(1, PromotionCampaign.MaxMoney).PrecisionScale(12, 0, true)
            .When(request => request.DiscountType == DiscountType.FixedAmount)
            .WithMessage("Số tiền giảm phải là số nguyên VNĐ lớn hơn 0");

        // Trần chỉ áp cho giảm trên đơn; giảm phí ship tới 100% (miễn phí) là bình thường.
        RuleFor(request => request.DiscountValue)
            .LessThanOrEqualTo(maxPercent)
            .When(request => request.Kind == PromotionKind.OrderDiscount && request.DiscountType == DiscountType.Percent)
            .WithMessage($"Mức giảm trên đơn tối đa {maxPercent}%");

        RuleFor(request => request.MaxDiscountAmount)
            .InclusiveBetween(1, PromotionCampaign.MaxMoney).PrecisionScale(12, 0, true)
            .When(request => request.MaxDiscountAmount is not null);
        RuleFor(request => request.MaxDiscountAmount).Null()
            .When(request => request.DiscountType == DiscountType.FixedAmount)
            .WithMessage("Mức giảm tối đa chỉ dùng khi giảm theo phần trăm");
        RuleFor(request => request.MinOrderAmount)
            .InclusiveBetween(0, PromotionCampaign.MaxMoney).PrecisionScale(12, 0, true);

        RuleFor(request => request.TargetIds)
            .Must(ids => ids is null or { Count: 0 })
            .When(request => request.ScopeType == PromotionScopeType.All)
            .WithMessage("Phạm vi \"tất cả sách\" không có danh sách đối tượng");
        RuleFor(request => request.TargetIds)
            .Must(ids => ids is { Count: > 0 and <= PromotionCampaign.MaxTargets })
            .When(request => request.ScopeType != PromotionScopeType.All)
            .WithMessage($"Phạm vi cần từ 1 đến {PromotionCampaign.MaxTargets} đối tượng");
        RuleForEach(request => request.TargetIds).GreaterThan(0);
        RuleFor(request => request)
            .Must(request => request.Kind == PromotionKind.OrderDiscount
                || (request.ScopeType == PromotionScopeType.All && !request.ExcludeOnSaleItems))
            .WithMessage("Chương trình giảm phí ship áp dụng cho cả đơn, không chọn phạm vi sách")
            .WithName("scopeType");

        RuleFor(request => request.UsageLimit).InclusiveBetween(1, 10_000_000).When(r => r.UsageLimit is not null);
        RuleFor(request => request.PerCustomerLimit).InclusiveBetween(1, 1000).When(r => r.PerCustomerLimit is not null);
        RuleFor(request => request.EndsAt).GreaterThan(request => request.StartsAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu");
        RuleFor(request => request.Priority).InclusiveBetween(0, MaxPriority);
    }
}

internal sealed class AdminPromotionsQueryValidator : PagedQueryValidator<AdminPromotionsQuery>
{
    public AdminPromotionsQueryValidator() => RuleFor(query => query.Q).MaximumLength(100);
}

internal sealed class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(Coupon.CodeMaxLength)
            .Must(code => CouponCodes.IsValid(CouponCodes.Normalize(code)))
            .WithMessage("Mã gồm 4–40 ký tự chữ, số, gạch nối hoặc gạch dưới");
        RuleFor(request => request.UsageLimit).InclusiveBetween(1, 10_000_000).When(r => r.UsageLimit is not null);
    }
}

internal sealed class GenerateCouponsRequestValidator : AbstractValidator<GenerateCouponsRequest>
{
    public const int MaxPrefixLength = 12;

    public GenerateCouponsRequestValidator(IOptions<PromotionOptions> options)
    {
        RuleFor(request => request.Count).InclusiveBetween(1, options.Value.MaxCouponsPerBatch);
        RuleFor(request => request.Prefix).MaximumLength(MaxPrefixLength)
            .Matches("^[A-Za-z0-9_-]*$").When(request => request.Prefix is not null)
            .WithMessage("Tiền tố chỉ gồm chữ, số, gạch nối hoặc gạch dưới");
        RuleFor(request => request.UsageLimit).InclusiveBetween(1, 10_000_000).When(r => r.UsageLimit is not null);
    }
}

internal sealed class UpdateCouponRequestValidator : AbstractValidator<UpdateCouponRequest>
{
    public UpdateCouponRequestValidator() =>
        RuleFor(request => request.UsageLimit).InclusiveBetween(1, 10_000_000).When(r => r.UsageLimit is not null);
}

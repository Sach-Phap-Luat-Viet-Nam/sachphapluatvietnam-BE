using FluentValidation;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Promotion.Application;

internal sealed class UpsertComboRequestValidator : AbstractValidator<UpsertComboRequest>
{
    public UpsertComboRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Combo.NameMaxLength);
        RuleFor(request => request.Slug).Must(ComboSlug.IsValid)
            .WithMessage("Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối");
        RuleFor(request => request.Description).MaximumLength(Combo.DescriptionMaxLength);
        RuleFor(request => request.ImageUrl).MaximumLength(Combo.ImageUrlMaxLength)
            .Must(BeHttpsUrl).When(request => !string.IsNullOrWhiteSpace(request.ImageUrl))
            .WithMessage("Ảnh combo phải là đường dẫn https");
        RuleFor(request => request.ComboPrice)
            .InclusiveBetween(1, PromotionCampaign.MaxMoney).PrecisionScale(12, 0, true)
            .WithMessage("Giá combo phải là số nguyên VNĐ lớn hơn 0");

        RuleFor(request => request.Items)
            .Must(items => items is { Count: >= Combo.MinItems and <= Combo.MaxItems })
            .WithMessage($"Combo gồm từ {Combo.MinItems} đến {Combo.MaxItems} sách khác nhau");
        RuleFor(request => request.Items)
            .Must(items => items is null || items.Select(item => item.SkuId).Distinct().Count() == items.Count)
            .WithMessage("Một SKU chỉ xuất hiện một lần trong combo, tăng số lượng thay vì thêm dòng");
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(row => row.SkuId).GreaterThan(0);
            item.RuleFor(row => row.Quantity).InclusiveBetween(1, Combo.MaxItemQuantity);
        });

        RuleFor(request => request.EndsAt).GreaterThan(request => request.StartsAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu");
    }

    private static bool BeHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class AdminCombosQueryValidator : PagedQueryValidator<AdminCombosQuery>
{
    public AdminCombosQueryValidator() => RuleFor(query => query.Q).MaximumLength(100);
}

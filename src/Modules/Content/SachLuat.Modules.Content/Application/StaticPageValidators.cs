using FluentValidation;
using SachLuat.Modules.Content.Domain;

namespace SachLuat.Modules.Content.Application;

internal sealed class UpsertStaticPageRequestValidator : AbstractValidator<UpsertStaticPageRequest>
{
    /// <summary>Chữ thường, số, gạch nối giữa các cụm: dùng thẳng làm đường dẫn trên Storefront.</summary>
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public UpsertStaticPageRequestValidator()
    {
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(StaticPage.SlugMaxLength)
            .Matches(SlugPattern).WithMessage("Đường dẫn chỉ gồm chữ thường không dấu, số và dấu gạch nối");
        RuleFor(request => request.Title).NotEmpty().MaximumLength(StaticPage.TitleMaxLength);
        RuleFor(request => request.Description).MaximumLength(StaticPage.DescriptionMaxLength);
        RuleFor(request => request.ContentHtml).NotEmpty().MaximumLength(StaticPage.ContentMaxLength);
    }
}

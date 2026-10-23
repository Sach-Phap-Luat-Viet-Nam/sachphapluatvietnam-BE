using FluentValidation;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application.Storefront;

internal sealed class BooksQueryValidator : PagedQueryValidator<BooksQuery>
{
    public BooksQueryValidator()
    {
        RuleFor(query => query.Q).MaximumLength(200);
        RuleFor(query => query.Type)
            .Must(value => SnakeCaseEnumConverter<BookType>.TryParse(value, out _))
            .When(query => query.Type is not null)
            .WithMessage("Loại sách không hợp lệ");
        RuleFor(query => query.Audience)
            .Must(value => SnakeCaseEnumConverter<TargetAudience>.TryParse(value, out _))
            .When(query => query.Audience is not null)
            .WithMessage("Đối tượng không hợp lệ");
        RuleFor(query => query.Cover)
            .Must(value => SnakeCaseEnumConverter<CoverType>.TryParse(value, out _))
            .When(query => query.Cover is not null)
            .WithMessage("Loại bìa không hợp lệ");
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(query => query.MinPrice ?? 0);
        RuleFor(query => query.Sort).Must(BookSorts.All.Contains).WithMessage("Kiểu sắp xếp không hợp lệ");
        RuleFor(query => query.LegalDocument).MaximumLength(50);
    }
}

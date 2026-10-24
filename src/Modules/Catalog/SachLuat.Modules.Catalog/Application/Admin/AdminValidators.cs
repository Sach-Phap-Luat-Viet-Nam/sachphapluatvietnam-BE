using FluentValidation;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Media;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application.Admin;

internal static class CatalogRules
{
    public static IRuleBuilderOptions<T, string> ValidSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(Slug.IsValid).WithMessage("Slug chỉ gồm chữ thường không dấu, số và dấu gạch nối");

    public static IRuleBuilderOptions<T, decimal> ValidVnd<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).LessThanOrEqualTo(999_999_999_999m)
            .Must(price => decimal.Truncate(price) == price).WithMessage("Giá phải là số nguyên VNĐ");
}

internal sealed class AdminBooksQueryValidator : PagedQueryValidator<AdminBooksQuery>
{
    public AdminBooksQueryValidator()
    {
        RuleFor(query => query.Q).MaximumLength(200);
        RuleFor(query => query.Status)
            .Must(value => SnakeCaseEnumConverter<BookStatus>.TryParse(value, out _))
            .When(query => query.Status is not null)
            .WithMessage("Trạng thái không hợp lệ");
    }
}

internal sealed class AdminSearchQueryValidator : PagedQueryValidator<AdminSearchQuery>
{
    public AdminSearchQueryValidator() => RuleFor(query => query.Q).MaximumLength(200);
}

internal sealed class UpsertBookRequestValidator : AbstractValidator<UpsertBookRequest>
{
    public UpsertBookRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Slug).ValidSlug();
        RuleFor(request => request.BookType).IsInEnum();
        RuleFor(request => request.Status).IsInEnum();
        RuleForEach(request => request.TargetAudiences).IsInEnum();
        RuleFor(request => request.MetaTitle).MaximumLength(255);
        RuleFor(request => request.MetaDescription).MaximumLength(500);
    }
}

internal sealed class UpsertEditionRequestValidator : AbstractValidator<UpsertEditionRequest>
{
    public UpsertEditionRequestValidator(IOptions<CloudinaryOptions> cloudinaryOptions)
    {
        var cloudinary = cloudinaryOptions.Value;

        RuleFor(request => request.Isbn).Must(isbn => Isbn13.TryNormalize(isbn, out _))
            .WithMessage("ISBN không hợp lệ (13 chữ số, bắt đầu bằng 978 hoặc 979, đúng số kiểm tra)");
        RuleFor(request => request.Publisher).NotEmpty().MaximumLength(255);
        RuleFor(request => request.PublishingDecisionNo).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Status).IsInEnum();
        RuleFor(request => request.RegistrationNo).MaximumLength(100);
        RuleFor(request => request.EditionName).MaximumLength(100);
        RuleFor(request => request.PublishYear).InclusiveBetween((short)1900, (short)2100);
        RuleFor(request => request.UpdatedToText).MaximumLength(500);
        RuleFor(request => request.Pages).GreaterThan(0);
        RuleFor(request => request.Size).MaximumLength(50);
        RuleFor(request => request.WeightGram).GreaterThan(0);
        RuleFor(request => request.CoverImageUrl).MaximumLength(500)
            .Must(cloudinary.AcceptsImageUrl).WithMessage("Ảnh bìa phải được tải lên Cloudinary của hệ thống");
        RuleFor(request => request.PreviewFileUrl).MaximumLength(500);
    }
}

internal sealed class CreateSkuRequestValidator : AbstractValidator<CreateSkuRequest>
{
    public CreateSkuRequestValidator()
    {
        RuleFor(request => request.SkuCode).NotEmpty().MaximumLength(Sku.SkuCodeMaxLength);
        RuleFor(request => request.CoverType).IsInEnum();
        RuleFor(request => request.ListPrice).ValidVnd();
        RuleFor(request => request.SalePrice).ValidVnd()
            .LessThanOrEqualTo(request => request.ListPrice).WithMessage("Giá bán không được vượt giá bìa");
    }
}

internal sealed class UpdateSkuRequestValidator : AbstractValidator<UpdateSkuRequest>
{
    public UpdateSkuRequestValidator()
    {
        RuleFor(request => request.ListPrice).ValidVnd();
        RuleFor(request => request.SalePrice).ValidVnd()
            .LessThanOrEqualTo(request => request.ListPrice).WithMessage("Giá bán không được vượt giá bìa");
    }
}

internal sealed class UpsertPriceScheduleRequestValidator : AbstractValidator<UpsertPriceScheduleRequest>
{
    public UpsertPriceScheduleRequestValidator()
    {
        RuleFor(request => request.SalePrice).ValidVnd();
        RuleFor(request => request.EndsAt).GreaterThan(request => request.StartsAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu");
        RuleFor(request => request.Note).MaximumLength(SkuPriceSchedule.NoteMaxLength);
    }
}

internal sealed class UpsertCategoryRequestValidator : AbstractValidator<UpsertCategoryRequest>
{
    public UpsertCategoryRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Slug).ValidSlug();
    }
}

internal sealed class UpsertAuthorRequestValidator : AbstractValidator<UpsertAuthorRequest>
{
    public UpsertAuthorRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Slug).ValidSlug();
    }
}

internal sealed class UpsertLegalDocumentRequestValidator : AbstractValidator<UpsertLegalDocumentRequest>
{
    public UpsertLegalDocumentRequestValidator()
    {
        RuleFor(request => request.DocNumber).NotEmpty().MaximumLength(50);
        RuleFor(request => request.DocType).IsInEnum();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(500);
        RuleFor(request => request.EffectiveDate).GreaterThanOrEqualTo(request => request.IssuedDate)
            .When(request => request.IssuedDate is not null && request.EffectiveDate is not null)
            .WithMessage("Ngày hiệu lực không được trước ngày ban hành");
    }
}

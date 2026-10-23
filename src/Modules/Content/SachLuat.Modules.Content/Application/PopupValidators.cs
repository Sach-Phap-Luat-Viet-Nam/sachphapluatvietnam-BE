using FluentValidation;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Content.Domain;
using SachLuat.SharedKernel.Media;

namespace SachLuat.Modules.Content.Application;

internal sealed class UpsertPopupRequestValidator : AbstractValidator<UpsertPopupRequest>
{
    public const int MaxPriority = 1000;

    public UpsertPopupRequestValidator(IOptions<CloudinaryOptions> cloudinaryOptions)
    {
        var cloudinary = cloudinaryOptions.Value;

        RuleFor(request => request.Title).NotEmpty().MaximumLength(Popup.TitleMaxLength);
        RuleFor(request => request.ImageUrl).NotEmpty().MaximumLength(Popup.UrlMaxLength)
            .Must(cloudinary.AcceptsImageUrl).WithMessage("Ảnh popup phải được tải lên Cloudinary của hệ thống");
        RuleFor(request => request.LinkUrl).MaximumLength(Popup.UrlMaxLength)
            .Must(IsSafeLink).When(request => !string.IsNullOrWhiteSpace(request.LinkUrl))
            .WithMessage("Link phải là đường dẫn trong site (bắt đầu bằng /) hoặc URL http/https");
        RuleFor(request => request.EndsAt).GreaterThan(request => request.StartsAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu");
        RuleFor(request => request.Priority).InclusiveBetween(0, MaxPriority);
        RuleFor(request => request.DisplayFrequency).IsInEnum();
    }

    // FE gán link vào href: chặn javascript:/data: (XSS) và "//host", "/\host" (trình duyệt hiểu là site khác).
    internal static bool IsSafeLink(string? link)
    {
        var value = link?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith('/'))
        {
            return value.Length == 1 || (value[1] != '/' && value[1] != '\\');
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}

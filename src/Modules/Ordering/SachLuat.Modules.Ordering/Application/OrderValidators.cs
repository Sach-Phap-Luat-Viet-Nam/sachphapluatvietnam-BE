using FluentValidation;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Application;

internal static class OrderRules
{
    // Số di động Việt Nam: 0xxxxxxxxx hoặc +84xxxxxxxxx.
    public const string PhonePattern = @"^(0|\+84)[0-9]{9}$";

    // Mã số thuế: 10 số, chi nhánh thêm "-xxx".
    public const string TaxCodePattern = @"^[0-9]{10}(-[0-9]{3})?$";

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches(PhonePattern).WithMessage("Số điện thoại không hợp lệ");
}

internal sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.CustomerName).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Phone).ValidPhone();
        RuleFor(request => request.Email).EmailAddress().MaximumLength(256)
            .When(request => !string.IsNullOrEmpty(request.Email));
        RuleFor(request => request.ShippingAddress).NotNull();
        RuleFor(request => request.ShippingAddress.Province).NotEmpty().MaximumLength(100)
            .When(request => request.ShippingAddress is not null);
        RuleFor(request => request.ShippingAddress.Ward).NotEmpty().MaximumLength(100)
            .When(request => request.ShippingAddress is not null);
        RuleFor(request => request.ShippingAddress.StreetAddress).NotEmpty().MaximumLength(500)
            .When(request => request.ShippingAddress is not null);
        RuleFor(request => request.PaymentMethod).IsInEnum();
        RuleFor(request => request.Note).MaximumLength(1000);
        When(request => request.InvoiceInfo is not null, () =>
        {
            RuleFor(request => request.InvoiceInfo!.CompanyName).NotEmpty().MaximumLength(255);
            RuleFor(request => request.InvoiceInfo!.TaxCode).NotEmpty().Matches(OrderRules.TaxCodePattern)
                .WithMessage("Mã số thuế không hợp lệ");
            RuleFor(request => request.InvoiceInfo!.Address).NotEmpty().MaximumLength(500);
            RuleFor(request => request.InvoiceInfo!.Email).NotEmpty().EmailAddress().MaximumLength(256);
        });
    }
}

internal sealed class LookupOrderRequestValidator : AbstractValidator<LookupOrderRequest>
{
    public LookupOrderRequestValidator()
    {
        RuleFor(request => request.OrderCode).NotEmpty().MaximumLength(20);
        RuleFor(request => request.Phone).ValidPhone();
    }
}

internal sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator() => RuleFor(request => request.Reason).MaximumLength(1000);
}

internal sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(request => request.Status).IsInEnum();
        RuleFor(request => request.Note).MaximumLength(1000);
    }
}

internal sealed class ShippingQuoteRequestValidator : AbstractValidator<ShippingQuoteRequest>
{
    public ShippingQuoteRequestValidator() => RuleFor(request => request.Subtotal).GreaterThanOrEqualTo(0);
}

internal sealed class AdminOrdersQueryValidator : PagedQueryValidator<AdminOrdersQuery>
{
    public AdminOrdersQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(value => SnakeCaseEnumConverter<OrderStatus>.TryParse(value, out _))
            .When(query => query.Status is not null)
            .WithMessage("Trạng thái đơn không hợp lệ");
        RuleFor(query => query.PaymentMethod)
            .Must(value => SnakeCaseEnumConverter<PaymentMethod>.TryParse(value, out _))
            .When(query => query.PaymentMethod is not null)
            .WithMessage("Phương thức thanh toán không hợp lệ");
        RuleFor(query => query.To).GreaterThanOrEqualTo(query => query.From)
            .When(query => query.From is not null && query.To is not null);
        RuleFor(query => query.Q).MaximumLength(50);
    }
}

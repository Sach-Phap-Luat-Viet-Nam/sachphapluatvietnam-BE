using FluentValidation;

namespace SachLuat.Modules.Cart.Application;

internal sealed record AddCartItemRequest(long SkuId, int Quantity);

internal sealed record UpdateCartItemRequest(int Quantity);

/// <param name="IsLatestEdition">false: hiện hộp xác nhận "Đã có ấn bản mới" (US-02).</param>
/// <param name="IsSellable">false: ấn bản ngừng bán, khách cần xóa khỏi giỏ trước khi đặt hàng.</param>
internal sealed record CartItemResponse(
    long SkuId,
    string SkuCode,
    string BookTitle,
    string BookSlug,
    string? EditionName,
    string CoverType,
    string? CoverImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal ListPrice,
    decimal LineTotal,
    bool IsLatestEdition,
    bool IsSellable,
    int AvailableQuantity);

internal sealed record CartResponse(IReadOnlyList<CartItemResponse> Items, int TotalQuantity, decimal Subtotal);

internal sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.SkuId).GreaterThan(0);
        RuleFor(request => request.Quantity).GreaterThan(0);
    }
}

internal sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() => RuleFor(request => request.Quantity).GreaterThanOrEqualTo(0);
}

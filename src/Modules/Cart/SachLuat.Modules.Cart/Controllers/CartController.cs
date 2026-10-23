using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Cart.Application;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Cart.Controllers;

/// <summary>Giỏ hàng; đăng nhập là tùy chọn (khách vãng lai dùng cookie).</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/cart")]
internal sealed class CartController(CartService cartService) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken cancellationToken) => cartService.GetAsync(cancellationToken);

    [HttpPost("items")]
    public Task<CartResponse> AddItem(AddCartItemRequest request, CancellationToken cancellationToken) =>
        cartService.AddItemAsync(request, cancellationToken);

    [HttpPatch("items/{skuId:long}")]
    public Task<CartResponse> UpdateItem(
        long skuId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken) => cartService.SetQuantityAsync(skuId, request.Quantity, cancellationToken);

    [HttpDelete("items/{skuId:long}")]
    public Task<CartResponse> RemoveItem(long skuId, CancellationToken cancellationToken) =>
        cartService.RemoveItemAsync(skuId, cancellationToken);

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartService.ClearAsync(cancellationToken);
        return NoContent();
    }
}

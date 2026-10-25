using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SachLuat.Modules.Ordering.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Ordering.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/orders")]
internal sealed class OrdersController(OrderService orderService) : ControllerBase
{
    /// <summary>Tạo đơn từ giỏ hiện tại; header <c>Idempotency-Key</c> chống tạo trùng khi bấm nhiều lần.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) => StatusCode(
            StatusCodes.Status201Created,
            await orderService.CreateAsync(request, idempotencyKey, cancellationToken));

    /// <summary>"Mua ngay": đặt thẳng một SKU, giỏ hàng giữ nguyên; <c>Idempotency-Key</c> như tạo đơn từ giỏ.</summary>
    [HttpPost("buy-now")]
    public async Task<ActionResult<OrderResponse>> BuyNow(
        BuyNowOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) => StatusCode(
            StatusCodes.Status201Created,
            await orderService.BuyNowAsync(request, idempotencyKey, cancellationToken));

    [Authorize]
    [HttpGet]
    public Task<PagedResponse<OrderSummaryResponse>> ListMine(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) => orderService.ListMineAsync(query, cancellationToken);

    [Authorize]
    [HttpGet("{code}")]
    public Task<OrderResponse> GetMine(string code, CancellationToken cancellationToken) =>
        orderService.GetMineAsync(code, cancellationToken);

    // Giới hạn tần suất để không dò mã đơn + số điện thoại.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("lookup")]
    public Task<OrderResponse> Lookup(LookupOrderRequest request, CancellationToken cancellationToken) =>
        orderService.LookupAsync(request, cancellationToken);

    [Authorize]
    [HttpPost("{code}/cancel")]
    public Task<OrderResponse> Cancel(string code, CancelOrderRequest request, CancellationToken cancellationToken) =>
        orderService.CancelMineAsync(code, request, cancellationToken);

    /// <summary>Khách vãng lai xem đơn qua link bí mật (<c>guestAccessToken</c> nhận lúc đặt hàng).</summary>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpGet("guest/{token}")]
    public Task<OrderResponse> GetGuest(string token, CancellationToken cancellationToken) =>
        orderService.GetGuestAsync(token, cancellationToken);

    /// <summary>Khách vãng lai tự hủy đơn qua link bí mật (BR-12).</summary>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("guest/{token}/cancel")]
    public Task<OrderResponse> CancelGuest(
        string token,
        CancelOrderRequest request,
        CancellationToken cancellationToken) => orderService.CancelGuestAsync(token, request, cancellationToken);
}

[ApiController]
[Route(ApiRoutes.V1 + "/shipping")]
internal sealed class ShippingController(OrderService orderService) : ControllerBase
{
    /// <summary>
    /// Phí ship tạm tính theo tỉnh và trọng lượng, miễn phí từ ngưỡng BR-14; thiếu <c>province</c> thì dùng phí cố định.
    /// </summary>
    [HttpPost("quote")]
    public Task<ShippingQuoteResponse> Quote(ShippingQuoteRequest request, CancellationToken cancellationToken) =>
        orderService.QuoteShippingAsync(request, cancellationToken);
}

[ApiController]
[Route(ApiRoutes.V1 + "/checkout")]
internal sealed class CheckoutController(OrderService orderService) : ControllerBase
{
    /// <summary>
    /// Xem trước tổng tiền (tạm tính, giảm giá, phí ship) cho giỏ hoặc "Mua ngay"; kèm thử mã giảm giá.
    /// Không ghi gì nên không trừ lượt dùng mã.
    /// </summary>
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [HttpPost("preview")]
    public Task<CheckoutPreviewResponse> Preview(
        CheckoutPreviewRequest request,
        CancellationToken cancellationToken) => orderService.PreviewAsync(request, cancellationToken);
}

[ApiController]
[Route(ApiRoutes.AdminV1 + "/orders")]
internal sealed class AdminOrdersController(OrderAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = Policies.OrdersRead)]
    [HttpGet]
    public Task<PagedResponse<OrderSummaryResponse>> List(
        [FromQuery] AdminOrdersQuery query,
        CancellationToken cancellationToken) => service.ListAsync(query, cancellationToken);

    [Authorize(Policy = Policies.OrdersRead)]
    [HttpGet("{code}")]
    public Task<AdminOrderResponse> Get(string code, CancellationToken cancellationToken) =>
        service.GetAsync(code, cancellationToken);

    [Authorize(Policy = Policies.OrdersManage)]
    [HttpPost("{code}/status")]
    public Task<AdminOrderResponse> ChangeStatus(
        string code,
        ChangeOrderStatusRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(currentUser.RequireUserId(), code, request, cancellationToken);
}

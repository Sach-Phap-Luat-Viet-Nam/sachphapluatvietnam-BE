using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Promotion.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Promotion.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/promotions")]
internal sealed class PromotionsController(PromotionStorefrontService service) : ControllerBase
{
    /// <summary>Ưu đãi đang chạy cho trang <c>/khuyen-mai</c>; mảng rỗng khi không có.</summary>
    [HttpGet("active")]
    public Task<IReadOnlyList<PublicPromotionResponse>> GetActive(CancellationToken cancellationToken) =>
        service.GetActiveAsync(cancellationToken);
}

[ApiController]
[Authorize(Policy = Policies.PricingAdmin)]
[Route(ApiRoutes.AdminV1 + "/promotions")]
internal sealed class AdminPromotionsController(PromotionAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminPromotionResponse>> List(
        [FromQuery] AdminPromotionsQuery query,
        CancellationToken cancellationToken) => service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminPromotionResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminPromotionResponse>> Create(
        UpsertPromotionRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AdminPromotionResponse> Update(
        long id,
        UpsertPromotionRequest request,
        CancellationToken cancellationToken) => service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:long}/stats")]
    public Task<PromotionStatsResponse> GetStats(long id, CancellationToken cancellationToken) =>
        service.GetStatsAsync(id, cancellationToken);

    [HttpGet("{id:long}/coupons")]
    public Task<PagedResponse<CouponResponse>> ListCoupons(
        long id,
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) => service.ListCouponsAsync(id, query, cancellationToken);

    [HttpPost("{id:long}/coupons")]
    public async Task<ActionResult<CouponResponse>> CreateCoupon(
        long id,
        CreateCouponRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateCouponAsync(id, request, cancellationToken));

    [HttpPost("{id:long}/coupons/generate")]
    public async Task<ActionResult<IReadOnlyList<CouponResponse>>> GenerateCoupons(
        long id,
        GenerateCouponsRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.GenerateCouponsAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize(Policy = Policies.PricingAdmin)]
[Route(ApiRoutes.AdminV1 + "/coupons")]
internal sealed class AdminCouponsController(PromotionAdminService service) : ControllerBase
{
    [HttpPut("{id:long}")]
    public Task<CouponResponse> Update(long id, UpdateCouponRequest request, CancellationToken cancellationToken) =>
        service.UpdateCouponAsync(id, request, cancellationToken);
}

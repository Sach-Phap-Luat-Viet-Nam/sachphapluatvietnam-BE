using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Promotion.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Promotion.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/combos")]
internal sealed class CombosController(ComboStorefrontService service) : ControllerBase
{
    /// <summary>Combo đang chạy; <c>skuId</c> chỉ lấy các combo có chứa SKU đó. Mảng rỗng khi không có.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ComboResponse>> List([FromQuery] long? skuId, CancellationToken cancellationToken) =>
        service.ListAsync(skuId, cancellationToken);

    [HttpGet("{slug}")]
    public Task<ComboResponse> Get(string slug, CancellationToken cancellationToken) =>
        service.GetAsync(slug, cancellationToken);
}

[ApiController]
[Authorize(Policy = Policies.PricingAdmin)]
[Route(ApiRoutes.AdminV1 + "/combos")]
internal sealed class AdminCombosController(ComboAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminComboResponse>> List(
        [FromQuery] AdminCombosQuery query,
        CancellationToken cancellationToken) => service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminComboResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminComboResponse>> Create(
        UpsertComboRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AdminComboResponse> Update(
        long id,
        UpsertComboRequest request,
        CancellationToken cancellationToken) => service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

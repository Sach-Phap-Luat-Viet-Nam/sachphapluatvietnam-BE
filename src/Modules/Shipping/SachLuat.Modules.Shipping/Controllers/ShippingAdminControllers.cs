using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Shipping.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Shipping.Controllers;

[ApiController]
[Authorize(Policy = Policies.ShippingAdmin)]
[Route(ApiRoutes.AdminV1 + "/shipping")]
internal sealed class AdminShippingController(ShippingAdminService service) : ControllerBase
{
    [HttpGet("zones")]
    public Task<IReadOnlyList<ZoneResponse>> ListZones(CancellationToken cancellationToken) =>
        service.ListZonesAsync(cancellationToken);

    [HttpPost("zones")]
    public async Task<ActionResult<ZoneResponse>> CreateZone(
        UpsertZoneRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateZoneAsync(request, cancellationToken));

    [HttpPut("zones/{id:long}")]
    public Task<ZoneResponse> UpdateZone(long id, UpsertZoneRequest request, CancellationToken cancellationToken) =>
        service.UpdateZoneAsync(id, request, cancellationToken);

    [HttpDelete("zones/{id:long}")]
    public async Task<IActionResult> DeleteZone(long id, CancellationToken cancellationToken)
    {
        await service.DeleteZoneAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("settings")]
    public Task<ShippingSettingsResponse> GetSettings(CancellationToken cancellationToken) =>
        service.GetSettingsAsync(cancellationToken);

    [HttpPut("settings")]
    public Task<ShippingSettingsResponse> UpdateSettings(
        ShippingSettingsRequest request,
        CancellationToken cancellationToken) => service.UpdateSettingsAsync(request, cancellationToken);
}

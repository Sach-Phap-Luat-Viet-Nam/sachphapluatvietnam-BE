using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me")]
internal sealed class MeController(ProfileService profileService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<MeResponse> Get(CancellationToken cancellationToken) =>
        profileService.GetAsync(currentUser.RequireUserId(), cancellationToken);

    [HttpPatch]
    public Task<MeResponse> Update(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        profileService.UpdateAsync(currentUser.RequireUserId(), request, cancellationToken);

    /// <summary>Lưu giao diện storefront (<c>dark</c>/<c>light</c>) của khách để ghi nhớ giữa các thiết bị.</summary>
    [HttpPut("preferences")]
    public Task<PreferencesResponse> UpdatePreferences(
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken) =>
        profileService.UpdatePreferencesAsync(currentUser.RequireUserId(), request, cancellationToken);

    [HttpGet("addresses")]
    public Task<IReadOnlyList<AddressResponse>> ListAddresses(CancellationToken cancellationToken) =>
        profileService.ListAddressesAsync(currentUser.RequireUserId(), cancellationToken);

    [HttpPost("addresses")]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> AddAddress(
        UpsertAddressRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await profileService.AddAddressAsync(currentUser.RequireUserId(), request, cancellationToken));

    [HttpPut("addresses/{id:long}")]
    public Task<IReadOnlyList<AddressResponse>> UpdateAddress(
        long id,
        UpsertAddressRequest request,
        CancellationToken cancellationToken) =>
        profileService.UpdateAddressAsync(currentUser.RequireUserId(), id, request, cancellationToken);

    [HttpPost("addresses/{id:long}/default")]
    public Task<IReadOnlyList<AddressResponse>> SetDefaultAddress(long id, CancellationToken cancellationToken) =>
        profileService.SetDefaultAddressAsync(currentUser.RequireUserId(), id, cancellationToken);

    [HttpDelete("addresses/{id:long}")]
    public async Task<IActionResult> DeleteAddress(long id, CancellationToken cancellationToken)
    {
        await profileService.DeleteAddressAsync(currentUser.RequireUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPut("invoice-info")]
    public Task<InvoiceInfoResponse> SaveInvoiceInfo(
        UpsertInvoiceInfoRequest request,
        CancellationToken cancellationToken) =>
        profileService.SaveInvoiceInfoAsync(currentUser.RequireUserId(), request, cancellationToken);

    [HttpDelete("invoice-info")]
    public async Task<IActionResult> DeleteInvoiceInfo(CancellationToken cancellationToken)
    {
        await profileService.DeleteInvoiceInfoAsync(currentUser.RequireUserId(), cancellationToken);
        return NoContent();
    }
}

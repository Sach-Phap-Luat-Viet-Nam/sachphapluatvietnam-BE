using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Content.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Media;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Content.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/popups")]
internal sealed class PopupsController(PopupService service) : ControllerBase
{
    /// <summary>Popup đang chạy; 204 khi không có.</summary>
    [HttpGet("active")]
    public async Task<ActionResult<PopupResponse>> GetActive(CancellationToken cancellationToken)
    {
        var popup = await service.GetActiveAsync(cancellationToken);
        return popup is null ? NoContent() : popup;
    }
}

[ApiController]
[Authorize(Policy = Policies.ContentAdmin)]
[Route(ApiRoutes.AdminV1 + "/popups")]
internal sealed class AdminPopupsController(PopupService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminPopupResponse>> List(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) => service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminPopupResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminPopupResponse>> Create(
        UpsertPopupRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AdminPopupResponse> Update(long id, UpsertPopupRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = Policies.ContentAdmin)]
[Route(ApiRoutes.AdminV1 + "/uploads")]
internal sealed class AdminContentUploadsController(CloudinaryUploadSigner signer, IOptions<CloudinaryOptions> options)
    : ControllerBase
{
    [HttpPost("popup-signature")]
    public UploadSignatureResponse CreatePopupSignature() => signer.CreateSignature(options.Value.PopupFolder);
}

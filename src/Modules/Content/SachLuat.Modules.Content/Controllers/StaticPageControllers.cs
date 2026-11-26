using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Content.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Content.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/pages")]
internal sealed class StaticPagesController(StaticPageService service) : ControllerBase
{
    /// <summary>Trang tĩnh đã xuất bản; 404 khi không có hoặc chưa xuất bản.</summary>
    [HttpGet("{slug}")]
    public Task<StaticPageResponse> Get(string slug, CancellationToken cancellationToken) =>
        service.GetPublishedAsync(slug, cancellationToken);
}

[ApiController]
[Authorize(Policy = Policies.ContentAdmin)]
[Route(ApiRoutes.AdminV1 + "/pages")]
internal sealed class AdminStaticPagesController(StaticPageService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminStaticPageSummaryResponse>> List(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) => service.ListAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminStaticPageResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminStaticPageResponse>> Create(
        UpsertStaticPageRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AdminStaticPageResponse> Update(
        long id,
        UpsertStaticPageRequest request,
        CancellationToken cancellationToken) => service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

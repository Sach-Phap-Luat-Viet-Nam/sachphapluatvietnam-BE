using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Media;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Catalog.Controllers;

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/books")]
internal sealed class AdminBooksController(CatalogAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminBookSummaryResponse>> List(
        [FromQuery] AdminBooksQuery query,
        CancellationToken cancellationToken) => service.ListBooksAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminBookResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetBookAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminBookResponse>> Create(
        UpsertBookRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateBookAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AdminBookResponse> Update(long id, UpsertBookRequest request, CancellationToken cancellationToken) =>
        service.UpdateBookAsync(id, request, cancellationToken);

    [HttpPost("{id:long}/editions")]
    public async Task<ActionResult<AdminBookResponse>> AddEdition(
        long id,
        UpsertEditionRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.AddEditionAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/editions")]
internal sealed class AdminEditionsController(CatalogAdminService service) : ControllerBase
{
    [HttpPut("{id:long}")]
    public Task<AdminBookResponse> Update(long id, UpsertEditionRequest request, CancellationToken cancellationToken) =>
        service.UpdateEditionAsync(id, request, cancellationToken);

    [HttpPost("{id:long}/set-latest")]
    public Task<AdminBookResponse> SetLatest(long id, CancellationToken cancellationToken) =>
        service.SetLatestEditionAsync(id, cancellationToken);

    [HttpPost("{id:long}/skus")]
    public async Task<ActionResult<AdminBookResponse>> AddSku(
        long id,
        CreateSkuRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.AddSkuAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/skus")]
internal sealed class AdminSkusController(CatalogAdminService service) : ControllerBase
{
    [HttpPut("{id:long}")]
    public Task<AdminBookResponse> Update(long id, UpdateSkuRequest request, CancellationToken cancellationToken) =>
        service.UpdateSkuAsync(id, request, cancellationToken);
}

[ApiController]
[Authorize(Policy = Policies.PricingAdmin)]
[Route(ApiRoutes.AdminV1)]
internal sealed class AdminPriceSchedulesController(PriceScheduleAdminService service) : ControllerBase
{
    [HttpGet("skus/{skuId:long}/price-schedules")]
    public Task<IReadOnlyList<PriceScheduleResponse>> List(long skuId, CancellationToken cancellationToken) =>
        service.ListAsync(skuId, cancellationToken);

    [HttpPost("skus/{skuId:long}/price-schedules")]
    public async Task<ActionResult<PriceScheduleResponse>> Create(
        long skuId,
        UpsertPriceScheduleRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(skuId, request, cancellationToken));

    [HttpPut("price-schedules/{id:long}")]
    public Task<PriceScheduleResponse> Update(
        long id,
        UpsertPriceScheduleRequest request,
        CancellationToken cancellationToken) => service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("price-schedules/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/categories")]
internal sealed class AdminCategoriesController(ReferenceDataAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryResponse>> List(CancellationToken cancellationToken) =>
        service.ListCategoriesAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        UpsertCategoryRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateCategoryAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<CategoryResponse> Update(long id, UpsertCategoryRequest request, CancellationToken cancellationToken) =>
        service.UpdateCategoryAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/authors")]
internal sealed class AdminAuthorsController(ReferenceDataAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AuthorResponse>> List([FromQuery] AdminSearchQuery query, CancellationToken cancellationToken) =>
        service.ListAuthorsAsync(query, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AuthorResponse>> Create(UpsertAuthorRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAuthorAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<AuthorResponse> Update(long id, UpsertAuthorRequest request, CancellationToken cancellationToken) =>
        service.UpdateAuthorAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAuthorAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/legal-documents")]
internal sealed class AdminLegalDocumentsController(ReferenceDataAdminService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<LegalDocumentResponse>> List(
        [FromQuery] AdminSearchQuery query,
        CancellationToken cancellationToken) => service.ListLegalDocumentsAsync(query, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<LegalDocumentResponse>> Create(
        UpsertLegalDocumentRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateLegalDocumentAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public Task<LegalDocumentResponse> Update(
        long id,
        UpsertLegalDocumentRequest request,
        CancellationToken cancellationToken) => service.UpdateLegalDocumentAsync(id, request, cancellationToken);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteLegalDocumentAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = Policies.CatalogAdmin)]
[Route(ApiRoutes.AdminV1 + "/uploads")]
internal sealed class AdminUploadsController(CloudinaryUploadSigner signer, IOptions<CloudinaryOptions> options)
    : ControllerBase
{
    [HttpPost("cover-signature")]
    public UploadSignatureResponse CreateCoverSignature() => signer.CreateSignature(options.Value.CoverFolder);
}

using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Catalog.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/books")]
internal sealed class BooksController(StorefrontQueryService queryService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<BookSummaryResponse>> List([FromQuery] BooksQuery query, CancellationToken cancellationToken) =>
        queryService.ListBooksAsync(query, cancellationToken);

    [HttpGet("{slug}")]
    public Task<BookDetailResponse> Get(string slug, CancellationToken cancellationToken) =>
        queryService.GetBookAsync(slug, cancellationToken);
}

[ApiController]
[Route(ApiRoutes.V1 + "/categories")]
internal sealed class CategoriesController(StorefrontQueryService queryService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryNode>> Tree(CancellationToken cancellationToken) =>
        queryService.GetCategoryTreeAsync(cancellationToken);
}

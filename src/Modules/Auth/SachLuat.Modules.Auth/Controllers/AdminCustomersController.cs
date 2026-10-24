using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Authorize(Policy = Policies.UsersAdmin)]
[Route(ApiRoutes.AdminV1 + "/customers")]
internal sealed class AdminCustomersController(UserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminUserResponse>> List(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        userAdminService.ListCustomersAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminCustomerResponse> Get(long id, CancellationToken cancellationToken) =>
        userAdminService.GetCustomerAsync(id, cancellationToken);
}

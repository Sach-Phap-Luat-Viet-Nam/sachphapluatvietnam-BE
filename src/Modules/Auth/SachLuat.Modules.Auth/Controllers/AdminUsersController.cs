using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Authorize(Policy = Policies.UsersAdmin)]
[Route(ApiRoutes.AdminV1 + "/users")]
internal sealed class AdminUsersController(UserAdminService userAdminService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<AdminUserResponse>> List(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        userAdminService.ListStaffAsync(query, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(CreateStaffRequest request) =>
        StatusCode(StatusCodes.Status201Created, await userAdminService.CreateStaffAsync(request));

    [HttpPut("{id:long}/roles")]
    public Task<AdminUserResponse> SetRoles(long id, SetRolesRequest request) =>
        userAdminService.SetRolesAsync(currentUser.RequireUserId(), id, request);

    [HttpPost("{id:long}/logout")]
    public Task<LogoutUserResponse> Logout(long id, CancellationToken cancellationToken) =>
        userAdminService.LogoutUserAsync(currentUser.RequireUserId(), id, cancellationToken);

    [HttpPost("{id:long}/lock")]
    public Task<AdminUserResponse> Lock(long id, CancellationToken cancellationToken) =>
        userAdminService.LockAsync(currentUser.RequireUserId(), id, cancellationToken);

    [HttpPost("{id:long}/unlock")]
    public Task<AdminUserResponse> Unlock(long id) =>
        userAdminService.UnlockAsync(currentUser.RequireUserId(), id);

    [HttpPost("{id:long}/password/reset")]
    public async Task<IActionResult> ResetPassword(
        long id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userAdminService.ResetPasswordAsync(currentUser.RequireUserId(), id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/impersonate")]
    public Task<AuthResponse> Impersonate(long id) =>
        userAdminService.ImpersonateAsync(currentUser.RequireUserId(), id);
}

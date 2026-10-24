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
    public Task<PagedResponse<UserResponse>> List(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        userAdminService.ListStaffAsync(query, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateStaffRequest request) =>
        StatusCode(StatusCodes.Status201Created, await userAdminService.CreateStaffAsync(request));

    [HttpPut("{id:long}/roles")]
    public Task<UserResponse> SetRoles(long id, SetRolesRequest request) =>
        userAdminService.SetRolesAsync(currentUser.RequireUserId(), id, request);

    [HttpPost("{id:long}/logout")]
    public Task<LogoutUserResponse> Logout(long id, CancellationToken cancellationToken) =>
        userAdminService.LogoutUserAsync(currentUser.RequireUserId(), id, cancellationToken);

    [HttpPut("{id:long}/password")]
    public async Task<IActionResult> ResetPassword(
        long id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userAdminService.ResetPasswordAsync(currentUser.RequireUserId(), id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/deactivate")]
    public Task<UserResponse> Deactivate(long id, CancellationToken cancellationToken) =>
        userAdminService.SetActiveAsync(currentUser.RequireUserId(), id, isActive: false, cancellationToken);

    [HttpPost("{id:long}/activate")]
    public Task<UserResponse> Activate(long id, CancellationToken cancellationToken) =>
        userAdminService.SetActiveAsync(currentUser.RequireUserId(), id, isActive: true, cancellationToken);

    [HttpPost("{id:long}/impersonate")]
    public Task<AuthResponse> Impersonate(long id, CancellationToken cancellationToken) =>
        userAdminService.ImpersonateAsync(currentUser.RequireUserId(), id, cancellationToken);
}

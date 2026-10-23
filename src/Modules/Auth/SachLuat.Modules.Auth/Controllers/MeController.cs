using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me")]
internal sealed class MeController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<UserResponse> Get() => authService.GetUserAsync(currentUser.RequireUserId());

    [HttpPatch]
    public Task<UserResponse> Update(UpdateProfileRequest request) =>
        authService.UpdateProfileAsync(currentUser.RequireUserId(), request);
}

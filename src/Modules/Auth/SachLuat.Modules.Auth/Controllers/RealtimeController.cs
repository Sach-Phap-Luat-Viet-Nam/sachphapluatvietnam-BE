using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PusherServer;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/realtime")]
internal sealed class RealtimeController(ICurrentUser currentUser, IPusher? pusher = null) : ControllerBase
{
    /// <summary>Ký quyền nghe kênh riêng <c>private-user-{id}</c>; chỉ chủ tài khoản được nghe kênh của mình.</summary>
    [HttpPost("auth")]
    public ActionResult Authorize(RealtimeAuthRequest request)
    {
        if (pusher is null)
        {
            throw AuthErrors.RealtimeUnavailable();
        }

        if (request.ChannelName != RealtimeChannels.ForUser(currentUser.RequireUserId()))
        {
            return Forbid();
        }

        return Content(pusher.Authenticate(request.ChannelName, request.SocketId).ToJson(), "application/json");
    }
}

internal sealed record RealtimeAuthRequest(string SocketId, string ChannelName);

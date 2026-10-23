using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SachLuat.Modules.Auth.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
internal sealed class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    // Cookie chỉ gửi kèm các request /api/v1/auth, JavaScript không đọc được (chống XSS lấy token).
    internal const string RefreshCookieName = "sl_refresh";
    private const string RefreshCookiePath = "/" + ApiRoutes.V1 + "/auth";

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var (response, refreshToken) = await authService.LoginAsync(request, cancellationToken);
        if (refreshToken is not null)
        {
            SetRefreshCookie(refreshToken);
        }

        return response;
    }

    [HttpPost("login/2fa")]
    public async Task<ActionResult<AuthResponse>> LoginTwoFactor(
        TwoFactorLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginTwoFactorAsync(request, cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return result.Response;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return result.Response;
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(expires: null));
        return NoContent();
    }

    [Authorize]
    [HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(currentUser.RequireUserId(), request, cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(expires: null));
        return NoContent();
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public Task<TwoFactorSetupResponse> SetupTwoFactor() =>
        authService.SetupTwoFactorAsync(currentUser.RequireUserId());

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<ActionResult<AuthResponse>> EnableTwoFactor(
        TwoFactorCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.EnableTwoFactorAsync(currentUser.RequireUserId(), request.Code, cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return result.Response;
    }

    private void SetRefreshCookie(IssuedRefreshToken refreshToken) => Response.Cookies.Append(
        RefreshCookieName,
        refreshToken.Token,
        CreateCookieOptions(refreshToken.IsPersistent ? refreshToken.ExpiresAt : null));

    private static CookieOptions CreateCookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
        IsEssential = true,
    };
}

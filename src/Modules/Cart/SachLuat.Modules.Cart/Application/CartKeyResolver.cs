using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Cart.Application;

/// <summary>
/// Xác định giỏ của request: thành viên theo user id, khách vãng lai theo cookie <see cref="CookieName"/>
/// (GUID ngẫu nhiên, HttpOnly).
/// </summary>
internal sealed class CartKeyResolver(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser,
    IOptions<CartOptions> options)
{
    public const string CookieName = "sl_cart";

    private readonly CartOptions _options = options.Value;

    // Cookie vừa tạo chỉ có trong response; giữ lại để các lần đọc sau trong cùng request thấy đúng giỏ.
    private Guid? _createdGuestId;

    public string? UserCartKey => currentUser.UserId is { } userId
        ? _options.KeyPrefix + "u:" + userId.ToString(CultureInfo.InvariantCulture)
        : null;

    public string? GuestCartKey => GuestId is { } guestId ? _options.KeyPrefix + "g:" + guestId.ToString("N") : null;

    private HttpContext HttpContext => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("Cart is only available within an HTTP request.");

    private Guid? GuestId => _createdGuestId
        ?? (Guid.TryParseExact(HttpContext.Request.Cookies[CookieName], "N", out var id) ? id : null);

    /// <summary>Key để ghi: thành viên dùng giỏ tài khoản; khách chưa có cookie thì tạo mới.</summary>
    public string GetOrCreateWritableKey()
    {
        if (UserCartKey is { } userKey)
        {
            return userKey;
        }

        if (GuestCartKey is { } guestKey)
        {
            return guestKey;
        }

        var guestId = Guid.NewGuid();
        _createdGuestId = guestId;
        HttpContext.Response.Cookies.Append(CookieName, guestId.ToString("N"), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/" + ApiRoutes.V1,
            Expires = DateTimeOffset.UtcNow.AddDays(_options.LifetimeDays),
            IsEssential = true,
        });
        return _options.KeyPrefix + "g:" + guestId.ToString("N");
    }

    public void DeleteGuestCookie()
    {
        _createdGuestId = null;
        HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" + ApiRoutes.V1 });
    }
}

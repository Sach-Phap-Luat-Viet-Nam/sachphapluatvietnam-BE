using System.Globalization;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Api.Security;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public long? UserId => ReadId(AuthClaimTypes.Subject);

    public long? ImpersonatorUserId => ReadId(AuthClaimTypes.ImpersonatorId);

    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;

    private long? ReadId(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}

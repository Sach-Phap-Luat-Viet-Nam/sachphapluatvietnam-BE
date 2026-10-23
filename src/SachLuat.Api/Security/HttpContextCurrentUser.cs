using System.Globalization;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Api.Security;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public long? UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.Subject)?.Value;
            return long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;
}

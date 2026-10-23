using Microsoft.AspNetCore.Identity;

namespace SachLuat.Modules.Auth.Domain;

internal sealed class AppRole : IdentityRole<long>
{
    public AppRole()
    {
    }

    public AppRole(string name)
        : base(name)
    {
    }
}

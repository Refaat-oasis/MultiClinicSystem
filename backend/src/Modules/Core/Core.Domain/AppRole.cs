using Microsoft.AspNetCore.Identity;

namespace Core.Domain;

/// <summary>
/// Staff realm role entity based on ASP.NET Core Identity.
/// </summary>
public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}

using Microsoft.AspNetCore.Identity;

namespace Core.Domain;

/// <summary>
/// Staff realm user representation based on ASP.NET Core Identity.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string Locale { get; set; } = "ar";
    public string Theme { get; set; } = "light";
    public Guid? AvatarAssetId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppUser()
    {
    }

    public AppUser(Guid id, string email, string fullName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email ?? string.Empty;
        UserName = email ?? string.Empty;
        FullName = fullName ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

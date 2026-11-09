namespace Core.Application;

/// <summary>
/// Result model describing authenticated user profile information.
/// </summary>
public sealed class UserInfoResult
{
    public Guid Id { get; }
    public string Email { get; }
    public string FullName { get; }
    public string Locale { get; }
    public string Theme { get; }
    public IReadOnlyList<string> Roles { get; }
    public Guid? ClinicId { get; }

    public UserInfoResult(
        Guid id,
        string email,
        string fullName,
        string locale,
        string theme,
        IReadOnlyList<string> roles,
        Guid? clinicId)
    {
        Id = id;
        Email = email ?? string.Empty;
        FullName = fullName ?? string.Empty;
        Locale = locale ?? "ar";
        Theme = theme ?? "light";
        Roles = roles ?? Array.Empty<string>();
        ClinicId = clinicId;
    }
}

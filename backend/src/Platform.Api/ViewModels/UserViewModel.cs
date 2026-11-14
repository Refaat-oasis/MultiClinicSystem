namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model describing authenticated user profile data.
/// </summary>
public sealed class UserViewModel
{
    public Guid Id { get; }
    public string Email { get; }
    public string FullName { get; }
    public string Locale { get; }
    public string Theme { get; }
    public IReadOnlyList<string> Roles { get; }
    public Guid? ClinicId { get; }

    public UserViewModel(
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

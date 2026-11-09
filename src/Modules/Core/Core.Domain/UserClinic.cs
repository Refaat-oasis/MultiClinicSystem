namespace Core.Domain;

/// <summary>
/// Links a staff user to a specific clinic tenant with an assigned role.
/// </summary>
public sealed class UserClinic
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Role { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private UserClinic()
    {
        Role = string.Empty;
    }

    public UserClinic(Guid id, Guid userId, Guid clinicId, string role, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        ClinicId = clinicId;
        Role = role ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
    }
}

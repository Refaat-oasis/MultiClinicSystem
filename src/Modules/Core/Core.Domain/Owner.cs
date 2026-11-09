namespace Core.Domain;

/// <summary>
/// Represents a clinic owner on the platform with an optional clinic quota.
/// </summary>
public sealed class Owner
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int? MaxClinics { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Owner()
    {
        Status = "Active";
    }

    public Owner(Guid id, Guid userId, int? maxClinics, DateTimeOffset createdAtUtc, string status = "Active")
    {
        Id = id;
        UserId = userId;
        MaxClinics = maxClinics;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public void UpdateMaxClinics(int? maxClinics)
    {
        MaxClinics = maxClinics;
    }

    public void Deactivate()
    {
        Status = "Suspended";
    }

    public void Activate()
    {
        Status = "Active";
    }
}

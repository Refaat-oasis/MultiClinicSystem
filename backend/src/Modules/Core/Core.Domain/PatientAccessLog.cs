namespace Core.Domain;

/// <summary>
/// Immutable audit row recorded whenever a platform admin or cross-clinic context accesses patient data.
/// </summary>
public sealed class PatientAccessLog
{
    public Guid Id { get; private set; }
    public string ActorId { get; private set; }
    public string ActorType { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public Guid? ClinicId { get; private set; }
    public string? Details { get; private set; }

    private PatientAccessLog()
    {
        ActorId = string.Empty;
        ActorType = "SuperAdmin";
        Reason = string.Empty;
    }

    public PatientAccessLog(Guid id, string actorId, string reason, DateTimeOffset timestampUtc, Guid? clinicId = null, string? details = null, string actorType = "SuperAdmin")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Access log ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Access reason must be documented.", nameof(reason));
        }

        Id = id;
        ActorId = actorId.Trim();
        ActorType = string.IsNullOrWhiteSpace(actorType) ? "SuperAdmin" : actorType.Trim();
        Reason = reason.Trim();
        TimestampUtc = timestampUtc;
        ClinicId = clinicId;
        Details = details;
    }
}


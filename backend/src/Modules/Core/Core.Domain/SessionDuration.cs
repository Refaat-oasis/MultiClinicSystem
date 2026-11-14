using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Append-only (idempotent per visit) recorded session duration sample for time-engine analytics.
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class SessionDuration : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public int RawMinutes { get; private set; }
    public int PlannedMinutes { get; private set; }
    public bool IsExcused { get; private set; }
    public string? ExcuseReason { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SessionDuration()
    {
    }

    public SessionDuration(
        Guid id,
        Guid clinicId,
        Guid visitId,
        Guid doctorId,
        Guid categoryId,
        int rawMinutes,
        int plannedMinutes,
        bool isExcused,
        string? excuseReason,
        DateTimeOffset occurredAtUtc,
        Guid? operationId,
        DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (visitId == Guid.Empty || doctorId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new ArgumentException("VisitId, DoctorId, and CategoryId are required.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        DoctorId = doctorId;
        CategoryId = categoryId;
        RawMinutes = Math.Max(1, rawMinutes);
        PlannedMinutes = Math.Max(5, plannedMinutes);
        IsExcused = isExcused;
        ExcuseReason = string.IsNullOrWhiteSpace(excuseReason) ? null : excuseReason.Trim();
        OccurredAtUtc = occurredAtUtc;
        OperationId = operationId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void SetExcused(bool isExcused, string? excuseReason, DateTimeOffset nowUtc)
    {
        if (isExcused && string.IsNullOrWhiteSpace(excuseReason))
        {
            throw new ArgumentException("ExcuseReason is required when marking a session excused.", nameof(excuseReason));
        }

        IsExcused = isExcused;
        ExcuseReason = isExcused ? excuseReason!.Trim() : null;
        UpdatedAtUtc = nowUtc;
    }

    public void Void(string reason, DateTimeOffset voidedAtUtc)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidReason = reason;
        UpdatedAtUtc = voidedAtUtc;
    }
}

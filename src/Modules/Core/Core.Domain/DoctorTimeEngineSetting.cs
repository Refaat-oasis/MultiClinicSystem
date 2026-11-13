using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-doctor toggle controlling whether the average-time engine adjusts scheduling durations.
/// ConflictClass: LastWriterWins.
/// </summary>
public sealed class DoctorTimeEngineSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid DoctorId { get; private set; }
    public bool IsEngineEnabled { get; private set; } = true;
    public Guid? OperationId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private DoctorTimeEngineSetting()
    {
    }

    public DoctorTimeEngineSetting(
        Guid id,
        Guid clinicId,
        Guid doctorId,
        bool isEngineEnabled,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty || doctorId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId and DoctorId are required.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        DoctorId = doctorId;
        IsEngineEnabled = isEngineEnabled;
        OperationId = operationId;
        UpdatedAtUtc = nowUtc;
    }

    public void SetEnabled(bool enabled, Guid? operationId, DateTimeOffset nowUtc)
    {
        IsEngineEnabled = enabled;
        OperationId = operationId;
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

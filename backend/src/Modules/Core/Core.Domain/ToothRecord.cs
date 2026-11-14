using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Historical dental chart record linked to a specific visit; latest record per tooth defines current state.
/// </summary>
public sealed class ToothRecord : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid PatientId { get; private set; }
    public Guid VisitId { get; private set; }
    public string FdiToothNumber { get; private set; } = string.Empty;
    public string UniversalToothCode { get; private set; } = string.Empty;
    public bool IsPrimaryTooth { get; private set; }
    public ToothCondition Condition { get; private set; }
    public string SurfacesJson { get; private set; } = "[]";
    public string? Notes { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ToothRecord()
    {
    }

    public ToothRecord(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid visitId,
        string fdiToothNumber,
        string universalToothCode,
        bool isPrimaryTooth,
        ToothCondition condition,
        string surfacesJson,
        string? notes,
        DateTimeOffset recordedAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        VisitId = visitId;
        FdiToothNumber = fdiToothNumber;
        UniversalToothCode = universalToothCode;
        IsPrimaryTooth = isPrimaryTooth;
        Condition = condition;
        SurfacesJson = string.IsNullOrWhiteSpace(surfacesJson) ? "[]" : surfacesJson;
        Notes = notes?.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinical laboratory or prosthodontic lab order tracked per visit.
/// </summary>
public sealed class LabOrder : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string LabName { get; private set; } = string.Empty;
    public string TestOrWorkName { get; private set; } = string.Empty;
    public string? ToothOrSite { get; private set; }
    public string? Instructions { get; private set; }
    public string Status { get; private set; } = "Ordered";
    public string? ResultSummary { get; private set; }
    public DateTimeOffset? ResultRecordedAtUtc { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private LabOrder()
    {
    }

    public LabOrder(
        Guid id,
        Guid clinicId,
        Guid visitId,
        Guid patientId,
        Guid doctorId,
        string labName,
        string testOrWorkName,
        string? toothOrSite,
        string? instructions,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        PatientId = patientId;
        DoctorId = doctorId;
        LabName = labName.Trim();
        TestOrWorkName = testOrWorkName.Trim();
        ToothOrSite = toothOrSite?.Trim();
        Instructions = instructions?.Trim();
        Status = "Ordered";
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public void RecordResult(string resultSummary, DateTimeOffset recordedAtUtc)
    {
        ResultSummary = resultSummary.Trim();
        ResultRecordedAtUtc = recordedAtUtc.ToUniversalTime();
        Status = "ResultRecorded";
    }
}


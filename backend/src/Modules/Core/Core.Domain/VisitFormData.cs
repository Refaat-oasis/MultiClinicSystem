using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Specialty form answers captured during a visit against a specific ClinicalForm version.
/// </summary>
public sealed class VisitFormData : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public Guid ClinicalFormId { get; private set; }
    public string SpecialtyCode { get; private set; } = string.Empty;
    public int FormVersion { get; private set; }
    public string AnswersJson { get; private set; } = "{}";
    public bool IsVoided { get; private set; }
    public DateTimeOffset SavedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private VisitFormData()
    {
    }

    public VisitFormData(
        Guid id,
        Guid clinicId,
        Guid visitId,
        Guid clinicalFormId,
        string specialtyCode,
        int formVersion,
        string answersJson,
        DateTimeOffset savedAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        ClinicalFormId = clinicalFormId;
        SpecialtyCode = specialtyCode.Trim().ToLowerInvariant();
        FormVersion = formVersion;
        AnswersJson = answersJson;
        SavedAtUtc = savedAtUtc.ToUniversalTime();
    }

    public void UpdateAnswers(string answersJson, DateTimeOffset savedAtUtc)
    {
        AnswersJson = answersJson;
        SavedAtUtc = savedAtUtc.ToUniversalTime();
    }
}


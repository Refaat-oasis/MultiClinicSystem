using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// ICD-10 diagnosis entry recorded for a visit.
/// </summary>
public sealed class VisitDiagnosis : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public string Icd10Code { get; private set; } = string.Empty;
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public string? Notes { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private VisitDiagnosis()
    {
    }

    public VisitDiagnosis(
        Guid id,
        Guid clinicId,
        Guid visitId,
        string icd10Code,
        string titleAr,
        string titleEn,
        bool isPrimary,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        Icd10Code = icd10Code.Trim().ToUpperInvariant();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        IsPrimary = isPrimary;
        Notes = notes?.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}


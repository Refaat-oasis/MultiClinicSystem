using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinical procedure performed during a visit (linked optionally to a tooth or anatomical site).
/// </summary>
public sealed class VisitProcedure : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public string ProcedureCode { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? ToothOrSite { get; private set; }
    public string? Notes { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private VisitProcedure()
    {
    }

    public VisitProcedure(
        Guid id,
        Guid clinicId,
        Guid visitId,
        string procedureCode,
        string nameAr,
        string nameEn,
        string? toothOrSite,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        ProcedureCode = procedureCode.Trim();
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        ToothOrSite = toothOrSite?.Trim();
        Notes = notes?.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}


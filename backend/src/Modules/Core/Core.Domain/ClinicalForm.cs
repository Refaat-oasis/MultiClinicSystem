using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Versioned JSON-Schema clinical form template scoped to a clinic and specialty.
/// </summary>
public sealed class ClinicalForm : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public string SpecialtyCode { get; private set; } = string.Empty;
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public string JsonSchema { get; private set; } = "{}";
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicalForm()
    {
    }

    public ClinicalForm(
        Guid id,
        Guid clinicId,
        string specialtyCode,
        string titleAr,
        string titleEn,
        string jsonSchema,
        int version,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        SpecialtyCode = specialtyCode.Trim().ToLowerInvariant();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        JsonSchema = jsonSchema;
        Version = Math.Max(1, version);
        IsActive = true;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public void Deactivate() => IsActive = false;
}


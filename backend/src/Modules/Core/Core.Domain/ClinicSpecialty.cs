using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Specialty configured for a clinic during onboarding or settings management, including specialty plugin defaults.
/// </summary>
public sealed class ClinicSpecialty : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string CatalogCode { get; private set; }
    public string NameAr { get; private set; }
    public string NameEn { get; private set; }
    public string DefaultFormSchemaJson { get; private set; }
    public string DefaultAppointmentCategoriesJson { get; private set; }
    public bool HasDentalPlugin { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicSpecialty()
    {
        CatalogCode = string.Empty;
        NameAr = string.Empty;
        NameEn = string.Empty;
        DefaultFormSchemaJson = "{}";
        DefaultAppointmentCategoriesJson = "[]";
    }

    public ClinicSpecialty(
        Guid id,
        Guid clinicId,
        string catalogCode,
        string nameAr,
        string nameEn,
        string defaultFormSchemaJson,
        string defaultAppointmentCategoriesJson,
        bool hasDentalPlugin,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Specialty ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(catalogCode))
        {
            throw new ArgumentException("Catalog code cannot be empty.", nameof(catalogCode));
        }

        Id = id;
        ClinicId = clinicId;
        CatalogCode = catalogCode.Trim().ToLowerInvariant();
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DefaultFormSchemaJson = string.IsNullOrWhiteSpace(defaultFormSchemaJson) ? "{}" : defaultFormSchemaJson;
        DefaultAppointmentCategoriesJson = string.IsNullOrWhiteSpace(defaultAppointmentCategoriesJson) ? "[]" : defaultAppointmentCategoriesJson;
        HasDentalPlugin = hasDentalPlugin;
        CreatedAtUtc = createdAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}


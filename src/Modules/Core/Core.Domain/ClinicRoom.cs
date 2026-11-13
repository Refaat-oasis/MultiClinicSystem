using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a consultation or procedure room within a clinic.
/// </summary>
public sealed class ClinicRoom : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public string NameEn { get; private set; }
    public string? SpecialtyCode { get; private set; }
    public string? FloorOrLocation { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicRoom()
    {
        Code = string.Empty;
        NameAr = string.Empty;
        NameEn = string.Empty;
    }

    public ClinicRoom(
        Guid id,
        Guid clinicId,
        string code,
        string nameAr,
        string nameEn,
        string? specialtyCode,
        string? floorOrLocation,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Room ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Room code cannot be empty.", nameof(code));
        }

        Id = id;
        ClinicId = clinicId;
        Code = code.Trim().ToUpperInvariant();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? Code : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? Code : nameEn.Trim();
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? null : specialtyCode.Trim().ToLowerInvariant();
        FloorOrLocation = string.IsNullOrWhiteSpace(floorOrLocation) ? null : floorOrLocation.Trim();
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(string code, string nameAr, string nameEn, string? specialtyCode, string? floorOrLocation, bool isActive)
    {
        Code = code.Trim().ToUpperInvariant();
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? null : specialtyCode.Trim().ToLowerInvariant();
        FloorOrLocation = string.IsNullOrWhiteSpace(floorOrLocation) ? null : floorOrLocation.Trim();
        IsActive = isActive;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsActive = false;
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}


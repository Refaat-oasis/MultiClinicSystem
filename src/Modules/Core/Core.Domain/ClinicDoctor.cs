using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a doctor profile configured within a clinic, optionally linked to an AppUser, specialties, and rooms.
/// </summary>
public sealed class ClinicDoctor : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? UserId { get; private set; }
    public string FullName { get; private set; }
    public string TitleAr { get; private set; }
    public string TitleEn { get; private set; }
    public string SpecialtyCodesJson { get; private set; }
    public string AssignedRoomIdsJson { get; private set; }
    public int SlotDurationMinutes { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicDoctor()
    {
        FullName = string.Empty;
        TitleAr = string.Empty;
        TitleEn = string.Empty;
        SpecialtyCodesJson = "[]";
        AssignedRoomIdsJson = "[]";
        SlotDurationMinutes = 20;
    }

    public ClinicDoctor(
        Guid id,
        Guid clinicId,
        Guid? userId,
        string fullName,
        string titleAr,
        string titleEn,
        string specialtyCodesJson,
        string assignedRoomIdsJson,
        int slotDurationMinutes,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Doctor ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Doctor name cannot be empty.", nameof(fullName));
        }

        Id = id;
        ClinicId = clinicId;
        UserId = userId;
        FullName = fullName.Trim();
        TitleAr = string.IsNullOrWhiteSpace(titleAr) ? FullName : titleAr.Trim();
        TitleEn = string.IsNullOrWhiteSpace(titleEn) ? FullName : titleEn.Trim();
        SpecialtyCodesJson = string.IsNullOrWhiteSpace(specialtyCodesJson) ? "[]" : specialtyCodesJson;
        AssignedRoomIdsJson = string.IsNullOrWhiteSpace(assignedRoomIdsJson) ? "[]" : assignedRoomIdsJson;
        SlotDurationMinutes = Math.Clamp(slotDurationMinutes, 5, 240);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(
        Guid? userId,
        string fullName,
        string titleAr,
        string titleEn,
        string specialtyCodesJson,
        string assignedRoomIdsJson,
        int slotDurationMinutes,
        bool isActive)
    {
        UserId = userId;
        FullName = fullName.Trim();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        SpecialtyCodesJson = string.IsNullOrWhiteSpace(specialtyCodesJson) ? "[]" : specialtyCodesJson;
        AssignedRoomIdsJson = string.IsNullOrWhiteSpace(assignedRoomIdsJson) ? "[]" : assignedRoomIdsJson;
        SlotDurationMinutes = Math.Clamp(slotDurationMinutes, 5, 240);
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

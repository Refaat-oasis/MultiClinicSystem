using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a clinic-scoped patient master record with Arabic-normalized search columns and medical history summary.
/// </summary>
public sealed class Patient : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string FileNumber { get; private set; }
    public string FullName { get; private set; }
    public string NormalizedName { get; private set; }
    public string Phone { get; private set; }
    public string NormalizedPhone { get; private set; }
    public string? SecondaryPhone { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public int? ApproximateAgeYears { get; private set; }
    public string Gender { get; private set; }
    public string? NationalId { get; private set; }
    public string? Address { get; private set; }
    public Guid? AssignedDoctorId { get; private set; }
    public string? PrimarySpecialtyCode { get; private set; }
    public bool IsMedicalHistoryReviewed { get; private set; }
    public string? MedicalHistorySummary { get; private set; }
    public string ChronicConditionsJson { get; private set; }
    public string CurrentMedicationsJson { get; private set; }
    public string PastSurgeriesJson { get; private set; }
    public DateTimeOffset? LastHistoryUpdatedAtUtc { get; private set; }
    public Guid? LastHistoryUpdatedByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Patient()
    {
        FileNumber = string.Empty;
        FullName = string.Empty;
        NormalizedName = string.Empty;
        Phone = string.Empty;
        NormalizedPhone = string.Empty;
        Gender = "Unspecified";
        ChronicConditionsJson = "[]";
        CurrentMedicationsJson = "[]";
        PastSurgeriesJson = "[]";
    }

    public Patient(
        Guid id,
        Guid clinicId,
        string fileNumber,
        string fullName,
        string phone,
        string? secondaryPhone,
        DateOnly? dateOfBirth,
        int? approximateAgeYears,
        string gender,
        string? nationalId,
        string? address,
        Guid? assignedDoctorId,
        string? primarySpecialtyCode,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(fileNumber))
        {
            throw new ArgumentException("Patient file number cannot be empty.", nameof(fileNumber));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Patient full name cannot be empty.", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Patient primary phone cannot be empty.", nameof(phone));
        }

        Id = id;
        ClinicId = clinicId;
        FileNumber = fileNumber.Trim();
        FullName = fullName.Trim();
        NormalizedName = ArabicNormalizer.NormalizeText(fullName);
        Phone = phone.Trim();
        NormalizedPhone = ArabicNormalizer.NormalizePhone(phone);
        SecondaryPhone = string.IsNullOrWhiteSpace(secondaryPhone) ? null : secondaryPhone.Trim();
        DateOfBirth = dateOfBirth;
        ApproximateAgeYears = dateOfBirth.HasValue ? null : approximateAgeYears;
        Gender = NormalizeGender(gender);
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        AssignedDoctorId = assignedDoctorId;
        PrimarySpecialtyCode = string.IsNullOrWhiteSpace(primarySpecialtyCode)
            ? null
            : primarySpecialtyCode.Trim().ToLowerInvariant();
        ChronicConditionsJson = "[]";
        CurrentMedicationsJson = "[]";
        PastSurgeriesJson = "[]";
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void UpdateDemographics(
        string fullName,
        string phone,
        string? secondaryPhone,
        DateOnly? dateOfBirth,
        int? approximateAgeYears,
        string gender,
        string? nationalId,
        string? address,
        Guid? assignedDoctorId,
        string? primarySpecialtyCode,
        DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Patient full name cannot be empty.", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Patient primary phone cannot be empty.", nameof(phone));
        }

        FullName = fullName.Trim();
        NormalizedName = ArabicNormalizer.NormalizeText(fullName);
        Phone = phone.Trim();
        NormalizedPhone = ArabicNormalizer.NormalizePhone(phone);
        SecondaryPhone = string.IsNullOrWhiteSpace(secondaryPhone) ? null : secondaryPhone.Trim();
        DateOfBirth = dateOfBirth;
        ApproximateAgeYears = dateOfBirth.HasValue ? null : approximateAgeYears;
        Gender = NormalizeGender(gender);
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        AssignedDoctorId = assignedDoctorId;
        PrimarySpecialtyCode = string.IsNullOrWhiteSpace(primarySpecialtyCode)
            ? null
            : primarySpecialtyCode.Trim().ToLowerInvariant();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateMedicalHistory(
        string? summary,
        string chronicConditionsJson,
        string currentMedicationsJson,
        string pastSurgeriesJson,
        bool markReviewed,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        MedicalHistorySummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        ChronicConditionsJson = string.IsNullOrWhiteSpace(chronicConditionsJson) ? "[]" : chronicConditionsJson;
        CurrentMedicationsJson = string.IsNullOrWhiteSpace(currentMedicationsJson) ? "[]" : currentMedicationsJson;
        PastSurgeriesJson = string.IsNullOrWhiteSpace(pastSurgeriesJson) ? "[]" : pastSurgeriesJson;
        if (markReviewed)
        {
            IsMedicalHistoryReviewed = true;
        }

        LastHistoryUpdatedByUserId = updatedByUserId;
        LastHistoryUpdatedAtUtc = updatedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
        UpdatedAtUtc = voidedAtUtc;
    }

    private static string NormalizeGender(string? gender)
    {
        if (string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase))
        {
            return "Male";
        }

        if (string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase))
        {
            return "Female";
        }

        return "Unspecified";
    }
}


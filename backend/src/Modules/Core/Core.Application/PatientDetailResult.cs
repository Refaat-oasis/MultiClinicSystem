namespace Core.Application;

/// <summary>
/// Detailed patient profile read model respecting module-off degradation (patient-history and attachments).
/// </summary>
public sealed record PatientDetailResult(
    Guid Id,
    Guid ClinicId,
    string FileNumber,
    string FullName,
    string Phone,
    string? SecondaryPhone,
    DateOnly? DateOfBirth,
    int? ApproximateAgeYears,
    int? AgeYears,
    string Gender,
    string? NationalId,
    string? Address,
    Guid? AssignedDoctorId,
    string? PrimarySpecialtyCode,
    bool IsMedicalHistoryEnabled,
    bool IsMedicalHistoryReviewed,
    string? MedicalHistorySummary,
    IReadOnlyList<string> ChronicConditions,
    IReadOnlyList<string> CurrentMedications,
    IReadOnlyList<string> PastSurgeries,
    IReadOnlyList<PatientAllergyItemResult> Allergies,
    IReadOnlyList<PatientNoteItemResult> Notes,
    bool IsAttachmentsEnabled,
    IReadOnlyList<PatientAttachmentItemResult> Attachments,
    IReadOnlyList<PatientDuplicateWarningResult> DuplicateWarnings,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);


namespace Core.Application;

/// <summary>
/// Read model for a clinical prescription and its allergy warning status.
/// </summary>
public sealed record PrescriptionResult(
    Guid Id,
    Guid VisitId,
    Guid PatientId,
    Guid DoctorId,
    string? Notes,
    bool HasAllergyWarning,
    string? AllergyWarningDetails,
    IReadOnlyList<PrescriptionItemResult> Items,
    DateTimeOffset CreatedAtUtc);


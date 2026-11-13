namespace Platform.Api.ViewModels;

public sealed record PrescriptionViewModel(
    Guid Id,
    Guid VisitId,
    Guid PatientId,
    Guid DoctorId,
    string? Notes,
    bool HasAllergyWarning,
    string? AllergyWarningDetails,
    IReadOnlyList<PrescriptionItemViewModel> Items,
    DateTimeOffset CreatedAtUtc);


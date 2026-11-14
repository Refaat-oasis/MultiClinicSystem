namespace Platform.Api.ViewModels;

public sealed record PortalMedicalBackgroundViewModel(
    IReadOnlyList<PatientAllergyItemViewModel> Allergies,
    IReadOnlyList<string> ChronicConditions,
    IReadOnlyList<string> CurrentMedications,
    IReadOnlyList<string> PastSurgeries,
    bool IsMedicalHistoryReviewed,
    string? MedicalHistorySummary,
    IReadOnlyList<PortalVisibleNoteItemViewModel> VisibleNotes,
    IReadOnlyList<PortalSharedAttachmentItemViewModel> SharedAttachments);


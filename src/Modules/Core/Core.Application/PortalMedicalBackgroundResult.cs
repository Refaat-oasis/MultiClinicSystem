namespace Core.Application;

public sealed record PortalMedicalBackgroundResult(
    IReadOnlyList<PatientAllergyItemResult> Allergies,
    IReadOnlyList<string> ChronicConditions,
    IReadOnlyList<string> CurrentMedications,
    IReadOnlyList<string> PastSurgeries,
    bool IsMedicalHistoryReviewed,
    string? MedicalHistorySummary,
    IReadOnlyList<PortalVisibleNoteItemResult> VisibleNotes,
    IReadOnlyList<PortalSharedAttachmentItemResult> SharedAttachments);


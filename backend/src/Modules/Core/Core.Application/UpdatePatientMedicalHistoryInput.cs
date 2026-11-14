namespace Core.Application;

/// <summary>
/// Input model for updating a patient's medical history and conditions.
/// </summary>
public sealed record UpdatePatientMedicalHistoryInput(
    string? Summary,
    IReadOnlyList<string> ChronicConditions,
    IReadOnlyList<string> CurrentMedications,
    IReadOnlyList<string> PastSurgeries,
    bool MarkReviewed);


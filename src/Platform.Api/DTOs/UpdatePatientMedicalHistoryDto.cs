namespace Platform.Api.DTOs;

public sealed record UpdatePatientMedicalHistoryDto(
    string? Summary,
    IReadOnlyList<string> ChronicConditions,
    IReadOnlyList<string> CurrentMedications,
    IReadOnlyList<string> PastSurgeries,
    bool MarkReviewed);


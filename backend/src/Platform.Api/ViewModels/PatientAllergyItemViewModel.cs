namespace Platform.Api.ViewModels;

public sealed record PatientAllergyItemViewModel(
    Guid Id,
    Guid PatientId,
    string Allergen,
    string Severity,
    string? Reaction,
    Guid RecordedByUserId,
    DateTimeOffset CreatedAtUtc);


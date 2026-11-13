namespace Platform.Api.ViewModels;

public sealed record PrescriptionItemViewModel(
    Guid Id,
    string DrugName,
    string? ActiveIngredient,
    string Dosage,
    string Frequency,
    string Duration,
    string? Instructions,
    bool TriggeredAllergyWarning);


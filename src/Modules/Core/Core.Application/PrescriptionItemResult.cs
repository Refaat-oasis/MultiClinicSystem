namespace Core.Application;

/// <summary>
/// Read model for an individual medication item on a prescription.
/// </summary>
public sealed record PrescriptionItemResult(
    Guid Id,
    string DrugName,
    string? ActiveIngredient,
    string Dosage,
    string Frequency,
    string Duration,
    string? Instructions,
    bool TriggeredAllergyWarning);


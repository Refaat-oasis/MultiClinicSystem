namespace Core.Application;

/// <summary>
/// Input model representing a single medication entry in a prescription.
/// </summary>
public sealed record PrescriptionItemInput(
    string DrugName,
    string? ActiveIngredient,
    string Dosage,
    string Frequency,
    string Duration,
    string? Instructions);


namespace Core.Application;

/// <summary>
/// Read model for an active patient allergy warning.
/// </summary>
public sealed record PatientAllergyItemResult(
    Guid Id,
    Guid PatientId,
    string Allergen,
    string Severity,
    string? Reaction,
    Guid RecordedByUserId,
    DateTimeOffset CreatedAtUtc);


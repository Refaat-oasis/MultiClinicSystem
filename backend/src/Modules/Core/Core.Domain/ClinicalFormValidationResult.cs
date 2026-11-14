namespace Core.Domain;

/// <summary>
/// Outcome of validating specialty visit form answers against a JSON Schema definition.
/// </summary>
public sealed record ClinicalFormValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);


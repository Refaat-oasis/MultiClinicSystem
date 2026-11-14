namespace Core.Application;

/// <summary>
/// Input model to issue a clinical prescription with explicit confirmation when an allergy conflict exists.
/// </summary>
public sealed record CreatePrescriptionInput(
    string? Notes,
    bool AcknowledgeAllergyWarning,
    IReadOnlyList<PrescriptionItemInput> Items,
    Guid? OperationId);


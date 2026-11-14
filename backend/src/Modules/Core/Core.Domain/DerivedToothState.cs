namespace Core.Domain;

/// <summary>
/// Latest derived clinical condition for a single tooth computed from historical visit tooth records.
/// </summary>
public sealed record DerivedToothState(
    string FdiToothNumber,
    string UniversalToothCode,
    bool IsPrimaryTooth,
    ToothCondition Condition,
    IReadOnlyList<string> Surfaces,
    string? Notes,
    Guid LastVisitId,
    DateTimeOffset RecordedAtUtc);


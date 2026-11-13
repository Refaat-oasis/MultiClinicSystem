namespace Core.Application;

/// <summary>
/// Read model for a tooth's latest derived clinical condition across all visits.
/// </summary>
public sealed record DerivedToothStateResult(
    string FdiToothNumber,
    string UniversalToothCode,
    bool IsPrimaryTooth,
    string Condition,
    IReadOnlyList<string> Surfaces,
    string? Notes,
    Guid LastVisitId,
    DateTimeOffset RecordedAtUtc);


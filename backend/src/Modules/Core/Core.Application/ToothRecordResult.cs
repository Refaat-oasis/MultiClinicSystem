namespace Core.Application;

/// <summary>
/// Read model for an individual historical tooth chart record recorded during a visit.
/// </summary>
public sealed record ToothRecordResult(
    Guid Id,
    Guid PatientId,
    Guid VisitId,
    string FdiToothNumber,
    string UniversalToothCode,
    bool IsPrimaryTooth,
    string Condition,
    IReadOnlyList<string> Surfaces,
    string? Notes,
    DateTimeOffset RecordedAtUtc);


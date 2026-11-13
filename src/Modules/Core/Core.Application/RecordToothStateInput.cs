namespace Core.Application;

/// <summary>
/// Input model to record a tooth chart condition change during a clinical dental visit.
/// </summary>
public sealed record RecordToothStateInput(
    string ToothCode,
    string? NumberingSystem,
    string Condition,
    IReadOnlyList<string>? Surfaces,
    string? Notes);


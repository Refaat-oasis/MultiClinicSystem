namespace Platform.Api.ViewModels;

public sealed record DerivedToothStateViewModel(
    string FdiToothNumber,
    string UniversalToothCode,
    bool IsPrimaryTooth,
    string Condition,
    IReadOnlyList<string> Surfaces,
    string? Notes,
    Guid LastVisitId,
    DateTimeOffset RecordedAtUtc);


namespace Platform.Api.ViewModels;

public sealed record ToothRecordViewModel(
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


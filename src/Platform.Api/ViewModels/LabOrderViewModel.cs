namespace Platform.Api.ViewModels;

public sealed record LabOrderViewModel(
    Guid Id,
    Guid VisitId,
    Guid PatientId,
    Guid DoctorId,
    string LabName,
    string TestOrWorkName,
    string? ToothOrSite,
    string? Instructions,
    string Status,
    string? ResultSummary,
    DateTimeOffset? ResultRecordedAtUtc,
    DateTimeOffset CreatedAtUtc);


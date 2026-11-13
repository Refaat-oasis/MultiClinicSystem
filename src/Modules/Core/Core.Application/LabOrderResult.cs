namespace Core.Application;

/// <summary>
/// Read model for a lab or prosthodontic work order associated with a visit.
/// </summary>
public sealed record LabOrderResult(
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


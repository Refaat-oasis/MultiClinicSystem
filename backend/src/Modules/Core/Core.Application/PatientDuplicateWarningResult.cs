namespace Core.Application;

/// <summary>
/// Duplicate patient candidate warning surfaced during duplicate-check or patient creation.
/// </summary>
public sealed record PatientDuplicateWarningResult(
    Guid PatientId,
    string FileNumber,
    string FullName,
    string Phone,
    string ReasonCode,
    int ConfidencePercent);


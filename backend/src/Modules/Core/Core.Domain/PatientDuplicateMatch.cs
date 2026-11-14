namespace Core.Domain;

/// <summary>
/// Result of matching a proposed patient against an existing clinic patient record.
/// </summary>
public sealed record PatientDuplicateMatch(
    Guid PatientId,
    string FileNumber,
    string FullName,
    string Phone,
    string ReasonCode,
    int ConfidencePercent);


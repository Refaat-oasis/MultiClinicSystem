namespace Core.Domain;

/// <summary>
/// Minimal projection used for in-memory duplicate patient scoring.
/// </summary>
public sealed record PatientDuplicateCandidate(
    Guid Id,
    string FileNumber,
    string FullName,
    string NormalizedName,
    string Phone,
    string NormalizedPhone);


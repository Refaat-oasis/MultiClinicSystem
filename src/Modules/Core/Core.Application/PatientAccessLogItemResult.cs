namespace Core.Application;

/// <summary>
/// Patient access audit row visible to clinic owners and platform security audits.
/// </summary>
public sealed record PatientAccessLogItemResult(
    Guid Id,
    string ActorId,
    string Reason,
    DateTimeOffset TimestampUtc,
    Guid? ClinicId,
    string? Details);


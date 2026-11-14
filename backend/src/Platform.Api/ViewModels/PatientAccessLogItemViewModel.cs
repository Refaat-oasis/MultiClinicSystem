namespace Platform.Api.ViewModels;

/// <summary>
/// Patient access log entry view model visible to clinic owners and platform security audits.
/// </summary>
public sealed record PatientAccessLogItemViewModel(
    Guid Id,
    string ActorId,
    string Reason,
    DateTimeOffset TimestampUtc,
    Guid? ClinicId,
    string? Details);


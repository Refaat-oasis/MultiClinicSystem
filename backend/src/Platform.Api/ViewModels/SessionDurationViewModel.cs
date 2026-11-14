namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for an individual recorded session duration row.
/// </summary>
public sealed record SessionDurationViewModel(
    Guid Id,
    Guid ClinicId,
    Guid VisitId,
    Guid DoctorId,
    Guid CategoryId,
    int RawMinutes,
    int PlannedMinutes,
    bool IsExcused,
    string? ExcuseReason,
    DateTimeOffset OccurredAtUtc);


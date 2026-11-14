namespace Core.Application;

/// <summary>
/// Read projection for an individual recorded session duration row.
/// </summary>
public sealed record SessionDurationResult(
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


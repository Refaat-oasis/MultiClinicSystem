namespace Core.Application;

/// <summary>
/// Super Admin read-only inspect snapshot of the time-engine configuration and computed statistics.
/// </summary>
public sealed record ClinicTimeEngineInspectResult(
    Guid ClinicId,
    ClinicTimeEngineSettingResult Settings,
    IReadOnlyList<DoctorTimeEngineSettingResult> DoctorSettings,
    IReadOnlyList<CategoryTimeStatResult> Stats,
    IReadOnlyList<SessionDurationResult> RecentDurations);


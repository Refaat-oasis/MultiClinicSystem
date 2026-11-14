namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for Super Admin read-only time-engine inspection.
/// </summary>
public sealed record ClinicTimeEngineInspectViewModel(
    Guid ClinicId,
    ClinicTimeEngineSettingViewModel Settings,
    IReadOnlyList<DoctorTimeEngineSettingViewModel> DoctorSettings,
    IReadOnlyList<CategoryTimeStatViewModel> Stats,
    IReadOnlyList<SessionDurationViewModel> RecentDurations);


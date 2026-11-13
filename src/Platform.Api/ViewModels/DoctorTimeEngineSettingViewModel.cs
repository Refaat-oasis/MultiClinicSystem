namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for a doctor's time-engine toggle setting.
/// </summary>
public sealed record DoctorTimeEngineSettingViewModel(
    Guid ClinicId,
    Guid DoctorId,
    string DoctorName,
    bool IsEngineEnabled,
    DateTimeOffset UpdatedAtUtc);


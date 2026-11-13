namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for clinic-level time-engine thresholds and step limits.
/// </summary>
public sealed record ClinicTimeEngineSettingViewModel(
    Guid ClinicId,
    int MinSampleSize,
    decimal TrimFraction,
    decimal MaxStepChangePercent,
    DateTimeOffset UpdatedAtUtc);


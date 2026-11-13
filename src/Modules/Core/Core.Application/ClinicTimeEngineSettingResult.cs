namespace Core.Application;

/// <summary>
/// Read projection for clinic-level time-engine thresholds and step limits.
/// </summary>
public sealed record ClinicTimeEngineSettingResult(
    Guid ClinicId,
    int MinSampleSize,
    decimal TrimFraction,
    decimal MaxStepChangePercent,
    DateTimeOffset UpdatedAtUtc);


namespace Core.Domain;

/// <summary>
/// Input parameters for computing trimmed average, suggested, and applied session duration.
/// </summary>
public sealed record TimeEngineCalculationInput(
    int DefaultDurationMinutes,
    int CurrentAppliedDurationMinutes,
    int MinSampleSize,
    decimal TrimFraction,
    decimal MaxStepChangePercent,
    bool IsDoctorEngineEnabled,
    IReadOnlyList<TimeEngineSample> Samples);


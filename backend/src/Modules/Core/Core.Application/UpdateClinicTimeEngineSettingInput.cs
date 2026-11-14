namespace Core.Application;

/// <summary>
/// Input model for updating clinic time-engine sample size, outlier trim fraction, and max step change percentage.
/// </summary>
public sealed record UpdateClinicTimeEngineSettingInput(
    int MinSampleSize,
    decimal TrimFraction,
    decimal MaxStepChangePercent,
    Guid? OperationId);


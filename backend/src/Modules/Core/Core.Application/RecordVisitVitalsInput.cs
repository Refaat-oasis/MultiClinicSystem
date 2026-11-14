namespace Core.Application;

/// <summary>
/// Input model to record patient vitals during a clinical visit.
/// </summary>
public sealed record RecordVisitVitalsInput(
    int? BloodPressureSystolic,
    int? BloodPressureDiastolic,
    int? PulseBpm,
    decimal? TemperatureCelsius,
    decimal? WeightKg,
    decimal? HeightCm,
    int? SpO2Percent,
    decimal? GlucoseMgDl);


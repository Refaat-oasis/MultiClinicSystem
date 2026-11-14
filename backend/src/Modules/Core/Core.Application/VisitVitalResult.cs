namespace Core.Application;

/// <summary>
/// Read model for recorded vital signs on a visit.
/// </summary>
public sealed record VisitVitalResult(
    Guid Id,
    Guid VisitId,
    int? BloodPressureSystolic,
    int? BloodPressureDiastolic,
    int? PulseBpm,
    decimal? TemperatureCelsius,
    decimal? WeightKg,
    decimal? HeightCm,
    decimal? Bmi,
    int? SpO2Percent,
    decimal? GlucoseMgDl,
    DateTimeOffset RecordedAtUtc);


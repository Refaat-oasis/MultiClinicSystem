namespace Platform.Api.ViewModels;

public sealed record VisitVitalViewModel(
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


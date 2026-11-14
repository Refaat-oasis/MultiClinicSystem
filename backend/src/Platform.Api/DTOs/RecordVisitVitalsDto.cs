namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to record patient vital signs for a clinical visit.
/// </summary>
public sealed class RecordVisitVitalsDto
{
    public int? BloodPressureSystolic { get; set; }
    public int? BloodPressureDiastolic { get; set; }
    public int? PulseBpm { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public int? SpO2Percent { get; set; }
    public decimal? GlucoseMgDl { get; set; }
}


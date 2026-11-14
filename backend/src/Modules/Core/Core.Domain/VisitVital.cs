using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Vital signs captured during a clinical visit with automatic BMI calculation.
/// </summary>
public sealed class VisitVital : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public int? BloodPressureSystolic { get; private set; }
    public int? BloodPressureDiastolic { get; private set; }
    public int? PulseBpm { get; private set; }
    public decimal? TemperatureCelsius { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? HeightCm { get; private set; }
    public decimal? Bmi { get; private set; }
    public int? SpO2Percent { get; private set; }
    public decimal? GlucoseMgDl { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private VisitVital()
    {
    }

    public VisitVital(
        Guid id,
        Guid clinicId,
        Guid visitId,
        int? bloodPressureSystolic,
        int? bloodPressureDiastolic,
        int? pulseBpm,
        decimal? temperatureCelsius,
        decimal? weightKg,
        decimal? heightCm,
        int? spO2Percent,
        decimal? glucoseMgDl,
        DateTimeOffset recordedAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        BloodPressureSystolic = bloodPressureSystolic;
        BloodPressureDiastolic = bloodPressureDiastolic;
        PulseBpm = pulseBpm;
        TemperatureCelsius = temperatureCelsius;
        WeightKg = weightKg;
        HeightCm = heightCm;
        Bmi = CalculateBmi(weightKg, heightCm);
        SpO2Percent = spO2Percent;
        GlucoseMgDl = glucoseMgDl;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
    }

    public static decimal? CalculateBmi(decimal? weightKg, decimal? heightCm)
    {
        if (!weightKg.HasValue || !heightCm.HasValue || weightKg <= 0m || heightCm <= 0m)
        {
            return null;
        }

        decimal heightMeters = heightCm.Value / 100m;
        return Math.Round(weightKg.Value / (heightMeters * heightMeters), 2, MidpointRounding.AwayFromZero);
    }
}


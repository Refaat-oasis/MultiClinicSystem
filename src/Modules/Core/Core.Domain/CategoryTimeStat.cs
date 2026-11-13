using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Learned time statistics per doctor and appointment category.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class CategoryTimeStat : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid DoctorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public int SampleCount { get; private set; }
    public int ExcusedCount { get; private set; }
    public decimal TrimmedAverageMinutes { get; private set; }
    public int SuggestedDurationMinutes { get; private set; }
    public int AppliedDurationMinutes { get; private set; }
    public int DefaultDurationMinutes { get; private set; }
    public bool IsDoctorEngineEnabled { get; private set; } = true;
    public bool IsAdopted { get; private set; }
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private CategoryTimeStat()
    {
    }

    public CategoryTimeStat(
        Guid id,
        Guid clinicId,
        Guid doctorId,
        Guid categoryId,
        int defaultDurationMinutes,
        bool isDoctorEngineEnabled,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty || doctorId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId, DoctorId, and CategoryId are required.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        DoctorId = doctorId;
        CategoryId = categoryId;
        DefaultDurationMinutes = Math.Clamp(defaultDurationMinutes, 5, 480);
        SuggestedDurationMinutes = DefaultDurationMinutes;
        AppliedDurationMinutes = DefaultDurationMinutes;
        IsDoctorEngineEnabled = isDoctorEngineEnabled;
        LastUpdatedAtUtc = nowUtc;
    }

    public void UpdateStats(
        TimeEngineCalculationResult calculation,
        int defaultDurationMinutes,
        bool isDoctorEngineEnabled,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        DefaultDurationMinutes = Math.Clamp(defaultDurationMinutes, 5, 480);
        SampleCount = calculation.SampleCount;
        ExcusedCount = calculation.ExcusedCount;
        TrimmedAverageMinutes = calculation.TrimmedAverageMinutes;
        SuggestedDurationMinutes = calculation.SuggestedDurationMinutes;
        AppliedDurationMinutes = calculation.AppliedDurationMinutes;
        IsDoctorEngineEnabled = isDoctorEngineEnabled;
        IsAdopted = calculation.IsAdopted;
        LastUpdatedAtUtc = nowUtc;
    }

    public void Void(string reason, DateTimeOffset voidedAtUtc)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidReason = reason;
        LastUpdatedAtUtc = voidedAtUtc;
    }
}

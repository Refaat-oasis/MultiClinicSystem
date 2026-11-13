using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Owner-managed clinic scheduling and waiting-queue policies (VIP jump limit, grace period, push limit, reschedule options).
/// </summary>
public sealed class ClinicSchedulingSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public int VipJumpLimit { get; private set; }
    public int LateGraceMinutes { get; private set; }
    public int MaxQueuePushes { get; private set; }
    public int DefaultSlotStepMinutes { get; private set; }
    public int MinBookingLeadMinutes { get; private set; }
    public string RescheduleOptionsJson { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicSchedulingSetting()
    {
        RescheduleOptionsJson = "[]";
    }

    public ClinicSchedulingSetting(
        Guid id,
        Guid clinicId,
        DateTimeOffset createdAtUtc,
        int vipJumpLimit = 2,
        int lateGraceMinutes = 10,
        int maxQueuePushes = 3,
        int defaultSlotStepMinutes = 15,
        int minBookingLeadMinutes = 0,
        string? rescheduleOptionsJson = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Setting ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        Id = id;
        ClinicId = clinicId;
        VipJumpLimit = Math.Clamp(vipJumpLimit, 0, 20);
        LateGraceMinutes = Math.Clamp(lateGraceMinutes, 0, 120);
        MaxQueuePushes = Math.Clamp(maxQueuePushes, 1, 10);
        DefaultSlotStepMinutes = Math.Clamp(defaultSlotStepMinutes, 5, 60);
        MinBookingLeadMinutes = Math.Clamp(minBookingLeadMinutes, 0, 1440);
        RescheduleOptionsJson = string.IsNullOrWhiteSpace(rescheduleOptionsJson)
            ? "[{\"key\":\"after_week\",\"labelAr\":\"بعد أسبوع\",\"labelEn\":\"After 1 week\",\"daysOffset\":7},{\"key\":\"after_month\",\"labelAr\":\"بعد شهر\",\"labelEn\":\"After 1 month\",\"daysOffset\":30},{\"key\":\"custom\",\"labelAr\":\"مخصص\",\"labelEn\":\"Custom\",\"daysOffset\":null}]"
            : rescheduleOptionsJson;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Update(
        int vipJumpLimit,
        int lateGraceMinutes,
        int maxQueuePushes,
        int defaultSlotStepMinutes,
        int minBookingLeadMinutes,
        string? rescheduleOptionsJson,
        DateTimeOffset now)
    {
        VipJumpLimit = Math.Clamp(vipJumpLimit, 0, 20);
        LateGraceMinutes = Math.Clamp(lateGraceMinutes, 0, 120);
        MaxQueuePushes = Math.Clamp(maxQueuePushes, 1, 10);
        DefaultSlotStepMinutes = Math.Clamp(defaultSlotStepMinutes, 5, 60);
        MinBookingLeadMinutes = Math.Clamp(minBookingLeadMinutes, 0, 1440);
        if (!string.IsNullOrWhiteSpace(rescheduleOptionsJson))
        {
            RescheduleOptionsJson = rescheduleOptionsJson;
        }

        UpdatedAtUtc = now;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

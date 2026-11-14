using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Owner-configured rules controlling patient self-booking and cancellation through the patient portal.
/// Conflict class: LastWriterWins (online-only).
/// </summary>
public sealed class ClinicPortalBookingSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public PortalBookingMode Mode { get; private set; }
    public int MinLeadTimeMinutes { get; private set; }
    public int MaxDaysAhead { get; private set; }
    public int SlotStepMinutes { get; private set; }
    public int MaxActiveFutureBookingsPerPatient { get; private set; }
    public int MaxBookingsPerDay { get; private set; }
    public int CancellationCutoffHours { get; private set; }
    public int AutoBlockAfterNoShows { get; private set; }
    public int ApprovalHoldHours { get; private set; }
    public bool ShowPricesWhenAvailable { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicPortalBookingSetting()
    {
    }

    public ClinicPortalBookingSetting(Guid id, Guid clinicId, DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        Mode = PortalBookingMode.AutoConfirm;
        MinLeadTimeMinutes = 120;
        MaxDaysAhead = 30;
        SlotStepMinutes = 15;
        MaxActiveFutureBookingsPerPatient = 2;
        MaxBookingsPerDay = 20;
        CancellationCutoffHours = 12;
        AutoBlockAfterNoShows = 3;
        ApprovalHoldHours = 12;
        ShowPricesWhenAvailable = true;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Update(
        PortalBookingMode mode,
        int minLeadTimeMinutes,
        int maxDaysAhead,
        int slotStepMinutes,
        int maxActiveFutureBookingsPerPatient,
        int maxBookingsPerDay,
        int cancellationCutoffHours,
        int autoBlockAfterNoShows,
        int approvalHoldHours,
        bool showPricesWhenAvailable,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minLeadTimeMinutes);
        if (maxDaysAhead is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDaysAhead), "MaxDaysAhead must be between 1 and 365.");
        }

        if (slotStepMinutes is < 5 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(slotStepMinutes), "SlotStepMinutes must be between 5 and 120.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxActiveFutureBookingsPerPatient, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBookingsPerDay, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(cancellationCutoffHours);
        ArgumentOutOfRangeException.ThrowIfNegative(autoBlockAfterNoShows);
        if (approvalHoldHours is < 1 or > 168)
        {
            throw new ArgumentOutOfRangeException(nameof(approvalHoldHours), "ApprovalHoldHours must be between 1 and 168.");
        }

        Mode = mode;
        MinLeadTimeMinutes = minLeadTimeMinutes;
        MaxDaysAhead = maxDaysAhead;
        SlotStepMinutes = slotStepMinutes;
        MaxActiveFutureBookingsPerPatient = maxActiveFutureBookingsPerPatient;
        MaxBookingsPerDay = maxBookingsPerDay;
        CancellationCutoffHours = cancellationCutoffHours;
        AutoBlockAfterNoShows = autoBlockAfterNoShows;
        ApprovalHoldHours = approvalHoldHours;
        ShowPricesWhenAvailable = showPricesWhenAvailable;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc.ToUniversalTime();
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

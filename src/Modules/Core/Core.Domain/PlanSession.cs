using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Individual session within a multi-session treatment plan with interval and allowed date window tracking.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class PlanSession : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid TreatmentPlanId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int IntervalDaysFromPrevious { get; private set; }
    public int WindowMinusDays { get; private set; }
    public int WindowPlusDays { get; private set; }
    public DateOnly TargetDate { get; private set; }
    public DateOnly EarliestAllowedDate { get; private set; }
    public DateOnly LatestAllowedDate { get; private set; }
    public DateOnly? ActualOrSlippedDate { get; private set; }
    public decimal SessionPriceAmount { get; private set; }
    public decimal MaterialsCostAmount { get; private set; }
    public Guid? LinkedAppointmentId { get; private set; }
    public Guid? LinkedVisitId { get; private set; }
    public Guid? LinkedChargeId { get; private set; }
    public PlanSessionStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PlanSession()
    {
    }

    public PlanSession(
        Guid id,
        Guid clinicId,
        Guid treatmentPlanId,
        int sequenceNumber,
        string title,
        PlanSessionWindowCalculation window,
        decimal sessionPriceAmount,
        decimal materialsCostAmount,
        string? notes,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Session title is required.", nameof(title));
        }

        Id = id;
        ClinicId = clinicId;
        TreatmentPlanId = treatmentPlanId;
        SequenceNumber = sequenceNumber;
        Title = title.Trim();
        IntervalDaysFromPrevious = window.IntervalDaysFromPrevious;
        WindowMinusDays = window.WindowMinusDays;
        WindowPlusDays = window.WindowPlusDays;
        TargetDate = window.TargetDate;
        EarliestAllowedDate = window.EarliestAllowedDate;
        LatestAllowedDate = window.LatestAllowedDate;
        SessionPriceAmount = Math.Max(0m, Math.Round(sessionPriceAmount, 2, MidpointRounding.AwayFromZero));
        MaterialsCostAmount = Math.Max(0m, Math.Round(materialsCostAmount, 2, MidpointRounding.AwayFromZero));
        Status = PlanSessionStatus.Planned;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void LinkAppointment(Guid appointmentId, DateTimeOffset nowUtc)
    {
        LinkedAppointmentId = appointmentId;
        if (Status == PlanSessionStatus.Planned || Status == PlanSessionStatus.Delayed)
        {
            Status = PlanSessionStatus.Scheduled;
        }
        UpdatedAtUtc = nowUtc;
    }

    public void ApplyRecalculatedWindow(
        PlanSessionWindowCalculation recalculated,
        bool markDelayed,
        DateOnly? slippedDate,
        DateTimeOffset nowUtc)
    {
        TargetDate = recalculated.TargetDate;
        EarliestAllowedDate = recalculated.EarliestAllowedDate;
        LatestAllowedDate = recalculated.LatestAllowedDate;
        if (slippedDate.HasValue)
        {
            ActualOrSlippedDate = slippedDate.Value;
        }
        if (markDelayed && Status != PlanSessionStatus.Completed)
        {
            Status = PlanSessionStatus.Delayed;
        }
        UpdatedAtUtc = nowUtc;
    }

    public void Complete(
        DateOnly completedDate,
        Guid? visitId,
        Guid? chargeId,
        decimal? additionalMaterialsCost,
        DateTimeOffset nowUtc)
    {
        ActualOrSlippedDate = completedDate;
        LinkedVisitId = visitId;
        LinkedChargeId = chargeId;
        if (additionalMaterialsCost.HasValue && additionalMaterialsCost.Value > 0m)
        {
            MaterialsCostAmount = Math.Round(MaterialsCostAmount + additionalMaterialsCost.Value, 2, MidpointRounding.AwayFromZero);
        }
        Status = PlanSessionStatus.Completed;
        UpdatedAtUtc = nowUtc;
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Multi-session patient treatment plan supporting fixed total or per-session billing modes.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class TreatmentPlan : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TreatmentPlanMode Mode { get; private set; }
    public decimal FixedTotalAmount { get; private set; }
    public decimal IncludedMaterialsCostAmount { get; private set; }
    public decimal UpfrontAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public TreatmentPlanStatus Status { get; private set; }
    public int TotalSessionsCount { get; private set; }
    public int CompletedSessionsCount { get; private set; }
    public bool ShowCostToPatient { get; private set; }
    public string? Notes { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private TreatmentPlan()
    {
    }

    public TreatmentPlan(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        string name,
        TreatmentPlanMode mode,
        decimal fixedTotalAmount,
        decimal includedMaterialsCostAmount,
        decimal upfrontAmount,
        string currency,
        int totalSessionsCount,
        bool showCostToPatient,
        string? notes,
        string createdByUserId,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalSessionsCount);

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        DoctorId = doctorId;
        Name = name.Trim();
        Mode = mode;
        FixedTotalAmount = Math.Max(0m, Math.Round(fixedTotalAmount, 2, MidpointRounding.AwayFromZero));
        IncludedMaterialsCostAmount = Math.Max(0m, Math.Round(includedMaterialsCostAmount, 2, MidpointRounding.AwayFromZero));
        UpfrontAmount = Math.Max(0m, Math.Round(upfrontAmount, 2, MidpointRounding.AwayFromZero));
        PaidAmount = 0m;
        RefundedAmount = 0m;
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        Status = TreatmentPlanStatus.Active;
        TotalSessionsCount = totalSessionsCount;
        CompletedSessionsCount = 0;
        ShowCostToPatient = showCostToPatient;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;
        OperationId = operationId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void AddMaterialsCost(decimal costDelta, DateTimeOffset nowUtc)
    {
        if (costDelta <= 0m)
        {
            return;
        }
        IncludedMaterialsCostAmount = Math.Round(IncludedMaterialsCostAmount + costDelta, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
    }

    public void RecordPayment(decimal amount, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);
        PaidAmount = Math.Round(PaidAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
    }

    public void RecordRefund(decimal amount, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);
        RefundedAmount = Math.Round(RefundedAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkSessionCompleted(int totalCompletedCount, DateTimeOffset nowUtc)
    {
        CompletedSessionsCount = Math.Clamp(totalCompletedCount, 0, TotalSessionsCount);
        Status = CompletedSessionsCount >= TotalSessionsCount ? TreatmentPlanStatus.Completed : TreatmentPlanStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void SetShowCostToPatient(bool show, DateTimeOffset nowUtc)
    {
        ShowCostToPatient = show;
        UpdatedAtUtc = nowUtc;
    }

    public decimal GetCompletionPercentage()
    {
        if (TotalSessionsCount <= 0)
        {
            return 0m;
        }
        return Math.Round((decimal)CompletedSessionsCount * 100m / TotalSessionsCount, 2, MidpointRounding.AwayFromZero);
    }

    public PlanBalanceSummary ComputeBalance(IReadOnlyList<decimal> postedPerSessionCharges) =>
        TreatmentPlanCalculator.ComputePlanBalance(Mode, FixedTotalAmount, postedPerSessionCharges, PaidAmount, RefundedAmount);
}

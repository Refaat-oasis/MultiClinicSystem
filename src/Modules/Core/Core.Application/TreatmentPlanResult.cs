using Core.Domain;

namespace Core.Application;

public sealed record TreatmentPlanResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid DoctorId,
    string Name,
    TreatmentPlanMode Mode,
    decimal FixedTotalAmount,
    decimal IncludedMaterialsCostAmount,
    decimal UpfrontAmount,
    decimal PaidAmount,
    decimal RefundedAmount,
    decimal TotalBilledOrContractedAmount,
    decimal RemainingBalanceAmount,
    string Currency,
    TreatmentPlanStatus Status,
    int TotalSessionsCount,
    int CompletedSessionsCount,
    int RemainingSessionsCount,
    decimal CompletionPercentage,
    bool ShowCostToPatient,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PlanSessionResult> Sessions,
    IReadOnlyList<PlanInstallmentResult> Installments);


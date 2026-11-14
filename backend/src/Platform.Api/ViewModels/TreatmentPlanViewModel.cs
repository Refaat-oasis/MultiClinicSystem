namespace Platform.Api.ViewModels;

public sealed record TreatmentPlanViewModel(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid DoctorId,
    string Name,
    string Mode,
    decimal FixedTotalAmount,
    decimal IncludedMaterialsCostAmount,
    decimal UpfrontAmount,
    decimal PaidAmount,
    decimal RefundedAmount,
    decimal TotalBilledOrContractedAmount,
    decimal RemainingBalanceAmount,
    string Currency,
    string Status,
    int TotalSessionsCount,
    int CompletedSessionsCount,
    int RemainingSessionsCount,
    decimal CompletionPercentage,
    bool ShowCostToPatient,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PlanSessionViewModel> Sessions,
    IReadOnlyList<PlanInstallmentViewModel> Installments);


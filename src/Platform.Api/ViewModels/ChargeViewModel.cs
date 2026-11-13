using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ChargeViewModel(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid? VisitId,
    Guid? AppointmentId,
    Guid? DoctorId,
    ChargeStatus Status,
    decimal StandardSessionPriceAmount,
    bool IsFreeSession,
    string? FreeSessionReason,
    decimal? CustomSessionPriceAmount,
    string? CustomPriceReason,
    decimal BaseSessionPriceAmount,
    decimal ToolsTotalCostAmount,
    decimal ToolsTotalChargedAmount,
    decimal ToolsMarkupAmount,
    decimal ExtraChargesAmount,
    string? ExtraChargeReason,
    decimal GrossSubtotalAmount,
    decimal DiscountAmount,
    string? DiscountReason,
    decimal UnroundedNetTotalAmount,
    decimal NetTotalAmount,
    decimal PaidAmount,
    decimal RefundedAmount,
    decimal RemainingDueAmount,
    string Currency,
    string RoundingRuleApplied,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ChargeLineViewModel> Lines);


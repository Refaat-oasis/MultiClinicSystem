using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record PayrollRunViewModel(
    Guid Id,
    Guid ClinicId,
    int PeriodYear,
    int PeriodMonth,
    PayrollRunStatus Status,
    PaymentMethod PaidFromMethod,
    decimal TotalBaseSalary,
    decimal TotalBonuses,
    decimal TotalDeductions,
    decimal TotalAdvances,
    decimal TotalNetPay,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinalizedAtUtc,
    string? Notes,
    IReadOnlyList<PayrollLineViewModel> Lines);


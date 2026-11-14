using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record DailyClosingViewModel(
    Guid Id,
    Guid ClinicId,
    DateOnly ClosingDate,
    DailyClosingStatus Status,
    bool FinanceExpensesExcluded,
    decimal TotalExpectedAmount,
    decimal TotalCountedAmount,
    decimal TotalVarianceAmount,
    string Currency,
    string ClosedByUserId,
    DateTimeOffset ClosedAtUtc,
    string? CloseNotes,
    string? ReopenedByUserId,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenReason,
    IReadOnlyList<ClosingMethodTotalViewModel> Methods);


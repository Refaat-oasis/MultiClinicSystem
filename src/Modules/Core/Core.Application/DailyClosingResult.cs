using Core.Domain;

namespace Core.Application;

public sealed record DailyClosingResult(
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
    IReadOnlyList<ClosingMethodTotalResult> Methods);


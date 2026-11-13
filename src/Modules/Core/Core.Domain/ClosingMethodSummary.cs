namespace Core.Domain;

/// <summary>
/// Computed expected/counted/variance summary for one payment method on daily cash closing.
/// </summary>
public sealed record ClosingMethodSummary(
    PaymentMethod Method,
    decimal PaymentsTotal,
    decimal RefundsTotal,
    decimal ExpensesTotal,
    decimal ExpectedAmount,
    decimal CountedAmount,
    decimal Variance);


using Core.Domain;

namespace Core.Application;

public sealed record ClosingMethodTotalResult(
    PaymentMethod Method,
    decimal PaymentsTotal,
    decimal RefundsTotal,
    decimal ExpensesTotal,
    decimal ExpectedTotal,
    decimal CountedTotal,
    decimal VarianceAmount,
    string Currency);


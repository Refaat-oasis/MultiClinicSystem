using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ClosingMethodTotalViewModel(
    PaymentMethod Method,
    decimal PaymentsTotal,
    decimal RefundsTotal,
    decimal ExpensesTotal,
    decimal ExpectedTotal,
    decimal CountedTotal,
    decimal VarianceAmount,
    string Currency);


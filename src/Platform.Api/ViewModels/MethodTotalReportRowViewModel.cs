using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record MethodTotalReportRowViewModel(
    PaymentMethod Method,
    decimal PaymentsIn,
    decimal RefundsOut,
    decimal ExpensesOut,
    decimal SupplierPaymentsOut,
    decimal NetFlow);


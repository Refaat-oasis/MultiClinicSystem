using Core.Domain;

namespace Core.Application;

public sealed record MethodTotalReportRowResult(
    PaymentMethod Method,
    decimal PaymentsIn,
    decimal RefundsOut,
    decimal ExpensesOut,
    decimal SupplierPaymentsOut,
    decimal NetFlow);


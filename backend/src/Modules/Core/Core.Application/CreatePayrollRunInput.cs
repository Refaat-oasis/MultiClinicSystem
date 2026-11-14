using Core.Domain;

namespace Core.Application;

public sealed record CreatePayrollRunInput(
    int PeriodYear,
    int PeriodMonth,
    PaymentMethod PaidFromMethod,
    string? Notes,
    Guid? OperationId);


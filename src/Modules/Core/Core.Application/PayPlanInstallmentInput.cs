using Core.Domain;

namespace Core.Application;

public sealed record PayPlanInstallmentInput(
    PaymentMethod Method,
    decimal Amount,
    string? Notes,
    Guid? OperationId);


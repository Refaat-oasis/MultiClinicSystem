using Core.Domain;

namespace Core.Application;

public sealed record CreateRefundInput(
    Guid? Id,
    Guid PatientId,
    Guid? OriginalPaymentId,
    Guid? ChargeId,
    PaymentMethod Method,
    decimal Amount,
    string Reason,
    Guid? OperationId);


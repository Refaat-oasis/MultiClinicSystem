using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CreateRefundDto(
    Guid? Id,
    Guid PatientId,
    Guid? OriginalPaymentId,
    Guid? ChargeId,
    PaymentMethod Method,
    decimal Amount,
    string Reason,
    Guid? OperationId);


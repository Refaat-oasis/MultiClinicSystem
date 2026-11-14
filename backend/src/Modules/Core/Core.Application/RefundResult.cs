using Core.Domain;

namespace Core.Application;

public sealed record RefundResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid? OriginalPaymentId,
    Guid? ChargeId,
    PaymentMethod Method,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReceiptNumber,
    string IssuedByUserId,
    DateTimeOffset RefundedAtUtc);


using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record RefundViewModel(
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


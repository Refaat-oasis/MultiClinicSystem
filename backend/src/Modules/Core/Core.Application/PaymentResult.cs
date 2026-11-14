using Core.Domain;

namespace Core.Application;

public sealed record PaymentResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid? ChargeId,
    Guid? DeviceId,
    string ReceiptNumber,
    PaymentMethod Method,
    decimal Amount,
    string Currency,
    bool IsDeposit,
    Guid? PaymentGroupId,
    string? Notes,
    string CollectedByUserId,
    DateTimeOffset PaidAtUtc);


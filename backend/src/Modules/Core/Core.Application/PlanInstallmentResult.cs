using Core.Domain;

namespace Core.Application;

public sealed record PlanInstallmentResult(
    Guid Id,
    int InstallmentNumber,
    bool IsUpfront,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Currency,
    PlanInstallmentStatus Status,
    Guid? LinkedPaymentId,
    DateTimeOffset? PaidAtUtc);


namespace Platform.Api.ViewModels;

public sealed record PlanInstallmentViewModel(
    Guid Id,
    int InstallmentNumber,
    bool IsUpfront,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Currency,
    string Status,
    Guid? LinkedPaymentId,
    DateTimeOffset? PaidAtUtc);


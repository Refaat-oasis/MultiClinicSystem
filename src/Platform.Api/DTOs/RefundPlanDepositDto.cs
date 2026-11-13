namespace Platform.Api.DTOs;

public sealed record RefundPlanDepositDto(
    decimal Amount,
    string Reason,
    Guid? OperationId);


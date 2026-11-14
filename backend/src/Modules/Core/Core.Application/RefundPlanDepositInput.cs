namespace Core.Application;

public sealed record RefundPlanDepositInput(
    decimal Amount,
    string Reason,
    Guid? OperationId);


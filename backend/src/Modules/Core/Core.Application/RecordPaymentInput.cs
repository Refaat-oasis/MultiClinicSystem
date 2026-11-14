namespace Core.Application;

public sealed record RecordPaymentInput(
    Guid? Id,
    Guid PatientId,
    Guid? ChargeId,
    bool IsDeposit,
    IReadOnlyList<PaymentSplitInput> Splits,
    Guid? OperationId);


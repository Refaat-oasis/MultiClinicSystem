namespace Platform.Api.DTOs;

public sealed record RecordPaymentDto(
    Guid? Id,
    Guid PatientId,
    Guid? ChargeId,
    bool IsDeposit,
    IReadOnlyList<PaymentSplitDto> Splits,
    Guid? OperationId);


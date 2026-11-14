namespace Platform.Api.DTOs;

public sealed record ReopenBillingDayDto(
    DateOnly ClosingDate,
    string Reason,
    Guid? OperationId);


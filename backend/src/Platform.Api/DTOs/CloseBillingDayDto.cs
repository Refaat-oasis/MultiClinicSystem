using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CloseBillingDayDto(
    DateOnly ClosingDate,
    IReadOnlyDictionary<PaymentMethod, decimal>? CountedByMethod,
    string? CloseNotes,
    Guid? OperationId);


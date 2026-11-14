using Core.Domain;

namespace Core.Application;

public sealed record CloseDayInput(
    DateOnly ClosingDate,
    IReadOnlyDictionary<PaymentMethod, decimal>? CountedByMethod,
    string? CloseNotes,
    Guid? OperationId);


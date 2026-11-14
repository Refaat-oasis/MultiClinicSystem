using Core.Domain;

namespace Core.Application;

public sealed record CreateExpenseInput(
    Guid? Id,
    string Category,
    string Description,
    PaymentMethod? Method,
    decimal Amount,
    Guid? SupplierId,
    bool CountsAsCashOut,
    DateTimeOffset? OccurredAtUtc,
    Guid? OperationId);


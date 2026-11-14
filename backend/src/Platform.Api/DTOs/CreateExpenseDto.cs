using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CreateExpenseDto(
    Guid? Id,
    string Category,
    string Description,
    PaymentMethod? Method,
    decimal Amount,
    Guid? SupplierId,
    bool CountsAsCashOut,
    DateTimeOffset? OccurredAtUtc,
    Guid? OperationId);


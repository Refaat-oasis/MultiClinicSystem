using Core.Domain;

namespace Core.Application;

public sealed record UpsertFixedCostInput(
    Guid? FixedCostId,
    FixedCostCategory Category,
    string TitleAr,
    string TitleEn,
    decimal Amount,
    FixedCostRecurrence Recurrence,
    PaymentMethod PaidFromMethod,
    DateOnly EffectiveFromDate,
    DateOnly? EffectiveToDate,
    bool IsActive,
    string? Notes,
    Guid? OperationId);


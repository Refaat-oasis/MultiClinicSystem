using Core.Domain;

namespace Core.Application;

public sealed record FixedCostOccurrenceResult(
    Guid FixedCostId,
    FixedCostCategory Category,
    string TitleAr,
    string TitleEn,
    DateOnly OccurrenceDate,
    decimal Amount,
    string Currency,
    PaymentMethod PaidFromMethod);


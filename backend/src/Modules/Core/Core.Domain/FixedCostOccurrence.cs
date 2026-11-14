namespace Core.Domain;

/// <summary>
/// Expanded dated occurrence of a clinic FixedCost definition.
/// </summary>
public sealed record FixedCostOccurrence(
    Guid FixedCostId,
    FixedCostCategory Category,
    string TitleAr,
    string TitleEn,
    DateOnly OccurrenceDate,
    decimal Amount,
    string Currency,
    PaymentMethod PaidFromMethod);


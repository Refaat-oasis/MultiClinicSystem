using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record FixedCostOccurrenceViewModel(
    Guid FixedCostId,
    FixedCostCategory Category,
    string TitleAr,
    string TitleEn,
    DateOnly OccurrenceDate,
    decimal Amount,
    string Currency,
    PaymentMethod PaidFromMethod);


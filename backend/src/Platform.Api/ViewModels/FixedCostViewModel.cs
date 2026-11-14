using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record FixedCostViewModel(
    Guid Id,
    Guid ClinicId,
    FixedCostCategory Category,
    string TitleAr,
    string TitleEn,
    decimal Amount,
    string Currency,
    FixedCostRecurrence Recurrence,
    PaymentMethod PaidFromMethod,
    DateOnly EffectiveFromDate,
    DateOnly? EffectiveToDate,
    bool IsActive,
    string? Notes);


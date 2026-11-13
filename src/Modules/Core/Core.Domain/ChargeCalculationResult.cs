namespace Core.Domain;

/// <summary>
/// Output summary of deterministic charge calculation including tool markup, discounts, and clinic rounding.
/// </summary>
public sealed record ChargeCalculationResult(
    decimal BaseSessionPrice,
    decimal ToolsTotalCost,
    decimal ToolsTotalCharged,
    decimal ToolsMarkupIncome,
    decimal ExtraChargesTotal,
    decimal GrossSubtotal,
    decimal DiscountAmount,
    decimal UnroundedNetTotal,
    decimal RoundedNetTotal);


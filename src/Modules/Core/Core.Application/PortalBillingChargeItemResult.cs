namespace Core.Application;

/// <summary>
/// Patient-portal-safe charge projection containing strictly charged totals and payments (never tool cost, markup, or lab cost).
/// </summary>
public sealed record PortalBillingChargeItemResult(
    Guid ChargeId,
    DateTimeOffset CreatedAtUtc,
    string Description,
    decimal TotalChargedAmount,
    decimal PaidAmount,
    decimal RemainingDueAmount,
    string Currency);


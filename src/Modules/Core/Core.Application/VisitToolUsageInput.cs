namespace Core.Application;

/// <summary>
/// Clinical session tool consumption item passed when completing a clinical visit.
/// </summary>
public sealed record VisitToolUsageInput(
    Guid InventoryItemId,
    decimal Quantity,
    decimal? PatientUnitPriceOverride = null);


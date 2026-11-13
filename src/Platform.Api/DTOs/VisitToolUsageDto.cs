namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO describing an inventory tool item consumed during a clinical visit.
/// </summary>
public sealed class VisitToolUsageDto
{
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PatientUnitPriceOverride { get; set; }
}


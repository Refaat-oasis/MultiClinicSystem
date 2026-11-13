namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO for creating or updating an inventory item.
/// </summary>
public sealed class UpsertInventoryItemDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitName { get; set; } = "unit";
    public int PackSize { get; set; } = 1;
    public decimal UnitCostAmount { get; set; }
    public decimal DefaultMarkupPercent { get; set; }
    public decimal LowStockThreshold { get; set; }
    public string? SpecialtyCode { get; set; }
    public string? StorageLocation { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? OperationId { get; set; }
}


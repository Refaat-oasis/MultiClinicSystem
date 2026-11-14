namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO for manually adjusting stock quantity under negative stock rules.
/// </summary>
public sealed class AdjustStockDto
{
    public Guid ItemId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal QuantityDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? OperationId { get; set; }
}


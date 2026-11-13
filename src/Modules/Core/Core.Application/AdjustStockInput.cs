namespace Core.Application;

/// <summary>
/// Input model for manually adjusting inventory stock up or down under the clinic's negative stock policy.
/// </summary>
public sealed record AdjustStockInput(
    Guid ItemId,
    Guid? BatchId,
    decimal QuantityDelta,
    string Reason,
    Guid? OperationId);


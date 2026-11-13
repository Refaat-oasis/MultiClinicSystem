namespace Core.Application;

/// <summary>
/// Read projection for an append-only inventory stock movement ledger row.
/// </summary>
public sealed record StockMovementResult(
    Guid Id,
    Guid ClinicId,
    Guid ItemId,
    string ItemCode,
    string ItemNameAr,
    string ItemNameEn,
    Guid? BatchId,
    string MovementType,
    decimal QuantityDelta,
    decimal BalanceAfter,
    decimal UnitCostAmount,
    string Currency,
    Guid? ReferenceVisitId,
    Guid? ReferencePurchaseInvoiceId,
    string? Reason,
    string CreatedByUserId,
    DateTimeOffset OccurredAtUtc);


namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for an append-only inventory stock movement entry.
/// </summary>
public sealed record StockMovementViewModel(
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


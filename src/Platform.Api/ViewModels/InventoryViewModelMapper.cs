using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Maps Core.Application inventory results into dedicated presentation ViewModels.
/// </summary>
public static class InventoryViewModelMapper
{
    public static InventoryItemViewModel ToViewModel(InventoryItemResult r) => new(
        r.Id,
        r.ClinicId,
        r.Code,
        r.NameAr,
        r.NameEn,
        r.UnitName,
        r.PackSize,
        r.UnitCostAmount,
        r.DefaultMarkupPercent,
        r.MarkedUpPatientUnitPrice,
        r.Currency,
        r.LowStockThreshold,
        r.QuantityOnHand,
        r.IsLowStock,
        r.SpecialtyCode,
        r.StorageLocation,
        r.IsActive,
        r.CreatedAtUtc,
        r.UpdatedAtUtc);

    public static InventoryItemBatchViewModel ToViewModel(InventoryItemBatchResult r) => new(
        r.Id,
        r.ClinicId,
        r.ItemId,
        r.ItemCode,
        r.ItemNameAr,
        r.ItemNameEn,
        r.BatchNumber,
        r.ExpiryDate,
        r.QuantityReceived,
        r.QuantityRemaining,
        r.UnitCostAmount,
        r.Currency,
        r.IsExpired,
        r.IsNearExpiry,
        r.PurchaseInvoiceId,
        r.CreatedAtUtc);

    public static StockMovementViewModel ToViewModel(StockMovementResult r) => new(
        r.Id,
        r.ClinicId,
        r.ItemId,
        r.ItemCode,
        r.ItemNameAr,
        r.ItemNameEn,
        r.BatchId,
        r.MovementType,
        r.QuantityDelta,
        r.BalanceAfter,
        r.UnitCostAmount,
        r.Currency,
        r.ReferenceVisitId,
        r.ReferencePurchaseInvoiceId,
        r.Reason,
        r.CreatedByUserId,
        r.OccurredAtUtc);

    public static PurchaseInvoiceViewModel ToViewModel(PurchaseInvoiceResult r) => new(
        r.Id,
        r.ClinicId,
        r.SupplierId,
        r.SupplierName,
        r.InvoiceNumber,
        r.InvoiceDate,
        r.TotalAmount,
        r.PaidAmount,
        r.RemainingAmount,
        r.Currency,
        r.Notes,
        r.RecordedByUserId,
        r.CreatedAtUtc,
        r.Lines.Select(l => new PurchaseInvoiceLineViewModel(
            l.Id,
            l.ItemId,
            l.ItemCode,
            l.ItemNameAr,
            l.ItemNameEn,
            l.BatchId,
            l.PackSize,
            l.PackCount,
            l.TotalUnits,
            l.PackPriceAmount,
            l.LineTotalAmount,
            l.UnitCostAmount,
            l.Currency,
            l.BatchNumber,
            l.ExpiryDate)).ToList());

    public static InventoryAlertViewModel ToViewModel(InventoryAlertResult r) => new(
        r.Id,
        r.ClinicId,
        r.ItemId,
        r.ItemCode,
        r.ItemNameAr,
        r.ItemNameEn,
        r.BatchId,
        r.Kind,
        r.MessageAr,
        r.MessageEn,
        r.IsResolved,
        r.CreatedAtUtc,
        r.ResolvedAtUtc);

    public static ClinicInventorySettingViewModel ToViewModel(ClinicInventorySettingResult r) => new(
        r.Id,
        r.ClinicId,
        r.NegativeStockPolicy,
        r.NearExpiryWarningDays,
        r.UpdatedAtUtc);

    public static SupplierInventoryStatementViewModel ToViewModel(SupplierInventoryStatementResult r) => new(
        r.SupplierId,
        r.SupplierName,
        r.TotalPurchasedAmount,
        r.TotalPaidAmount,
        r.BalanceDueAmount,
        r.FinanceLedgerAccruedAmount,
        r.FinanceLedgerPaidAmount,
        r.FinanceLedgerBalanceDueAmount,
        r.FinanceModuleEnabled,
        r.Currency,
        r.Invoices.Select(ToViewModel).ToList());

    public static ClinicInventoryInspectViewModel ToViewModel(ClinicInventoryInspectResult r) => new(
        r.ClinicId,
        ToViewModel(r.Settings),
        r.Items.Select(ToViewModel).ToList(),
        r.Batches.Select(ToViewModel).ToList(),
        r.Alerts.Select(ToViewModel).ToList(),
        r.RecentInvoices.Select(ToViewModel).ToList());
}

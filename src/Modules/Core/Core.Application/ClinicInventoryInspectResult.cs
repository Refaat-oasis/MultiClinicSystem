namespace Core.Application;

/// <summary>
/// Super Admin read-only inspect snapshot of a clinic's inventory catalog, batches, alerts, and recent invoices.
/// </summary>
public sealed record ClinicInventoryInspectResult(
    Guid ClinicId,
    ClinicInventorySettingResult Settings,
    IReadOnlyList<InventoryItemResult> Items,
    IReadOnlyList<InventoryItemBatchResult> Batches,
    IReadOnlyList<InventoryAlertResult> Alerts,
    IReadOnlyList<PurchaseInvoiceResult> RecentInvoices);


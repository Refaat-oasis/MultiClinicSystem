namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for Super Admin read-only inventory inspection.
/// </summary>
public sealed record ClinicInventoryInspectViewModel(
    Guid ClinicId,
    ClinicInventorySettingViewModel Settings,
    IReadOnlyList<InventoryItemViewModel> Items,
    IReadOnlyList<InventoryItemBatchViewModel> Batches,
    IReadOnlyList<InventoryAlertViewModel> Alerts,
    IReadOnlyList<PurchaseInvoiceViewModel> RecentInvoices);


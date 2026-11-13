using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Domain;

namespace Core.Application;

/// <summary>
/// Service contract for clinic inventory items, FEFO batch consumption, pack purchases, supplier statements, and alerts.
/// </summary>
public interface IInventoryService
{
    Task<Result<IReadOnlyList<InventoryItemResult>>> ListItemsAsync(
        Guid userId,
        Guid clinicId,
        string? query,
        string? specialtyCode,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryItemResult>> UpsertItemAsync(
        Guid userId,
        Guid clinicId,
        UpsertInventoryItemInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<InventoryItemBatchResult>>> ListBatchesAsync(
        Guid userId,
        Guid clinicId,
        Guid? itemId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StockMovementResult>>> ListMovementsAsync(
        Guid userId,
        Guid clinicId,
        Guid? itemId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseInvoiceResult>> ReceivePurchaseInvoiceAsync(
        Guid userId,
        Guid clinicId,
        ReceivePurchaseInvoiceInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PurchaseInvoiceResult>>> ListPurchaseInvoicesAsync(
        Guid userId,
        Guid clinicId,
        Guid? supplierId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<StockMovementResult>> AdjustStockAsync(
        Guid userId,
        Guid clinicId,
        AdjustStockInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<InventoryAlertResult>>> ListAlertsAsync(
        Guid userId,
        Guid clinicId,
        bool includeResolved,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<InventoryAlertResult>> ResolveAlertAsync(
        Guid userId,
        Guid clinicId,
        Guid alertId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicInventorySettingResult>> GetSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicInventorySettingResult>> UpdateSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicInventorySettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierInventoryStatementResult>> GetSupplierStatementAsync(
        Guid userId,
        Guid clinicId,
        Guid supplierId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ChargeToolLineInput>>> ConsumeVisitToolsAsync(
        Guid clinicId,
        Guid visitId,
        string actorUserId,
        IReadOnlyList<VisitToolUsageInput> toolsUsed,
        Guid? operationId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicInventoryInspectResult>> InspectClinicInventoryAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}

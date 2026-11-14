namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for an inventory item card/row.
/// </summary>
public sealed record InventoryItemViewModel(
    Guid Id,
    Guid ClinicId,
    string ItemCode,
    string NameAr,
    string NameEn,
    string UnitName,
    int PackSize,
    decimal UnitCostAmount,
    decimal DefaultMarkupPercent,
    decimal MarkedUpPatientUnitPrice,
    string Currency,
    decimal LowStockThreshold,
    decimal QuantityOnHand,
    bool IsLowStock,
    string? SpecialtyCode,
    string? StorageLocation,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

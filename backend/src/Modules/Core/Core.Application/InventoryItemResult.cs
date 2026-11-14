namespace Core.Application;

/// <summary>
/// Read projection for an inventory item including pack pricing, marked-up patient unit price, and low-stock state.
/// </summary>
public sealed record InventoryItemResult(
    Guid Id,
    Guid ClinicId,
    string Code,
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


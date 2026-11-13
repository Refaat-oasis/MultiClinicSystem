namespace Core.Application;

/// <summary>
/// Input model for creating or updating an inventory stock item with pack definition and markup rules.
/// </summary>
public sealed record UpsertInventoryItemInput(
    Guid? Id,
    string Code,
    string NameAr,
    string NameEn,
    string UnitName,
    int PackSize,
    decimal UnitCostAmount,
    decimal DefaultMarkupPercent,
    decimal LowStockThreshold,
    string? SpecialtyCode,
    string? StorageLocation,
    bool IsActive,
    Guid? OperationId);


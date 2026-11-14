namespace Core.Application;

/// <summary>
/// Read projection for an inventory alert (low stock, negative stock, near expiry, or expired batch).
/// </summary>
public sealed record InventoryAlertResult(
    Guid Id,
    Guid ClinicId,
    Guid ItemId,
    string ItemCode,
    string ItemNameAr,
    string ItemNameEn,
    Guid? BatchId,
    string Kind,
    string MessageAr,
    string MessageEn,
    bool IsResolved,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);


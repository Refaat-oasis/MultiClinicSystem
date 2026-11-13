namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for an inventory low-stock, negative-stock, or expiry alert.
/// </summary>
public sealed record InventoryAlertViewModel(
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


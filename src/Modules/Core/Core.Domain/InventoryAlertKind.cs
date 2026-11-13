namespace Core.Domain;

/// <summary>
/// Category of automated inventory warning raised to clinic staff and owner alerts feed.
/// </summary>
public enum InventoryAlertKind
{
    LowStock = 0,
    NearExpiry = 1,
    NegativeStock = 2
}


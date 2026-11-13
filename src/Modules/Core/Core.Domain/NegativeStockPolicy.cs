namespace Core.Domain;

/// <summary>
/// Controls whether stock consumption below zero is blocked or allowed with an audited warning.
/// </summary>
public enum NegativeStockPolicy
{
    Block = 0,
    AllowWithWarning = 1
}


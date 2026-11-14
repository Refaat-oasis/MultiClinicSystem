namespace Core.Domain;

/// <summary>
/// Append-only patient ledger entry categories.
/// </summary>
public enum LedgerEntryType
{
    Charge = 0,
    Payment = 1,
    Refund = 2,
    Adjustment = 3,
    Reversal = 4
}


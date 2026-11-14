namespace Core.Domain;

/// <summary>
/// Lifecycle status of a billing charge.
/// </summary>
public enum ChargeStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2
}


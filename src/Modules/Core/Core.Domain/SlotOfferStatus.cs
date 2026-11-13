namespace Core.Domain;

/// <summary>
/// Status of a freed-slot offer sent to a waiting-list patient.
/// </summary>
public enum SlotOfferStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3
}


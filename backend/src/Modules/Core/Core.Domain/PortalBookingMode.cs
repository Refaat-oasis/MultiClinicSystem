namespace Core.Domain;

/// <summary>
/// Booking confirmation mode configured per clinic for patient portal self-booking.
/// </summary>
public enum PortalBookingMode
{
    AutoConfirm = 0,
    RequiresApproval = 1
}

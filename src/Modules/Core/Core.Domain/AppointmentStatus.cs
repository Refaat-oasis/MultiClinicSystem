namespace Core.Domain;

/// <summary>
/// Appointment lifecycle status within the scheduling state machine.
/// </summary>
public enum AppointmentStatus
{
    Booked = 0,
    Arrived = 1,
    InSession = 2,
    Completed = 3,
    NoShow = 4,
    Cancelled = 5,
    Late = 6
}


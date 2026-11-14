namespace Core.Domain;

/// <summary>
/// Enforces valid appointment lifecycle transitions and slot-occupancy classification.
/// </summary>
public static class AppointmentStateMachine
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Booked] =
        [
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow,
            AppointmentStatus.Late
        ],
        [AppointmentStatus.Late] =
        [
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        ],
        [AppointmentStatus.Arrived] =
        [
            AppointmentStatus.InSession,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        ],
        [AppointmentStatus.InSession] =
        [
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled
        ],
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.NoShow] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.Requested] =
        [
            AppointmentStatus.Booked,
            AppointmentStatus.Declined,
            AppointmentStatus.Cancelled
        ],
        [AppointmentStatus.Declined] = []
    };

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out AppointmentStatus[]? next)
               && Array.IndexOf(next, to) >= 0;
    }

    public static IReadOnlyList<AppointmentStatus> GetAllowedTransitions(AppointmentStatus from)
    {
        return AllowedTransitions.TryGetValue(from, out AppointmentStatus[]? next)
            ? next
            : Array.Empty<AppointmentStatus>();
    }

    /// <summary>
    /// Returns true if the appointment occupies a doctor/room calendar slot.
    /// Cancelled, Declined, and NoShow release the slot. Requested holds the slot while pending approval.
    /// </summary>
    public static bool IsOccupyingStatus(AppointmentStatus status)
    {
        return status is AppointmentStatus.Booked
            or AppointmentStatus.Arrived
            or AppointmentStatus.InSession
            or AppointmentStatus.Completed
            or AppointmentStatus.Late
            or AppointmentStatus.Requested;
    }

    /// <summary>
    /// Evaluates slot occupancy accounting for unapproved portal request expiry.
    /// </summary>
    public static bool IsOccupying(Appointment appointment, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (!IsOccupyingStatus(appointment.Status))
        {
            return false;
        }

        return !appointment.IsPortalRequestExpired(nowUtc);
    }
}


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
        [AppointmentStatus.Cancelled] = []
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
    /// Cancelled and NoShow release the slot.
    /// </summary>
    public static bool IsOccupyingStatus(AppointmentStatus status)
    {
        return status is AppointmentStatus.Booked
            or AppointmentStatus.Arrived
            or AppointmentStatus.InSession
            or AppointmentStatus.Completed
            or AppointmentStatus.Late;
    }
}


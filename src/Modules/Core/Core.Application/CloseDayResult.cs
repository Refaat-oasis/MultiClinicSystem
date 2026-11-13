namespace Core.Application;

/// <summary>
/// Result returned when closing a date or range, listing affected active appointments for rescheduling.
/// </summary>
public sealed record CloseDayResult(
    Guid DayOffId,
    string StartDate,
    string EndDate,
    IReadOnlyList<AppointmentItemResult> AffectedAppointments);


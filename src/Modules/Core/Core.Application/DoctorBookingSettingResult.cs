namespace Core.Application;

/// <summary>
/// Read model for per-doctor online booking configuration.
/// </summary>
public sealed record DoctorBookingSettingResult(
    Guid DoctorId,
    bool AcceptsOnlineBooking,
    int? MaxDailyOnlineBookings);


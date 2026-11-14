namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for doctor online booking configuration.
/// </summary>
public sealed record DoctorBookingSettingViewModel(
    Guid DoctorId,
    bool AcceptsOnlineBooking,
    int? MaxDailyOnlineBookings);


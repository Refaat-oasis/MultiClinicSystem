namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to configure a doctor's online booking settings.
/// </summary>
public sealed class UpdateDoctorBookingSettingDto
{
    public bool AcceptsOnlineBooking { get; set; }
    public int? MaxDailyOnlineBookings { get; set; }
}


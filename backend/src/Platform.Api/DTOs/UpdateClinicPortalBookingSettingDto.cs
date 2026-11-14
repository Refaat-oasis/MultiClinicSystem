namespace Platform.Api.DTOs;

public sealed class UpdateClinicPortalBookingSettingDto
{
    public string Mode { get; set; } = "AutoConfirm";
    public int MinLeadTimeMinutes { get; set; } = 120;
    public int MaxDaysAhead { get; set; } = 30;
    public int SlotStepMinutes { get; set; } = 15;
    public int MaxActiveFutureBookingsPerPatient { get; set; } = 2;
    public int MaxBookingsPerDay { get; set; } = 20;
    public int CancellationCutoffHours { get; set; } = 12;
    public int AutoBlockAfterNoShows { get; set; } = 3;
    public int ApprovalHoldHours { get; set; } = 12;
    public bool ShowPricesWhenAvailable { get; set; } = true;
}

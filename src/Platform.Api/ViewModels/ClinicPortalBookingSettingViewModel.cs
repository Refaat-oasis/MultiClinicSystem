namespace Platform.Api.ViewModels;

public sealed record ClinicPortalBookingSettingViewModel(
    Guid ClinicId,
    string Mode,
    int MinLeadTimeMinutes,
    int MaxDaysAhead,
    int SlotStepMinutes,
    int MaxActiveFutureBookingsPerPatient,
    int MaxBookingsPerDay,
    int CancellationCutoffHours,
    int AutoBlockAfterNoShows,
    int ApprovalHoldHours,
    bool ShowPricesWhenAvailable,
    DateTimeOffset UpdatedAtUtc);

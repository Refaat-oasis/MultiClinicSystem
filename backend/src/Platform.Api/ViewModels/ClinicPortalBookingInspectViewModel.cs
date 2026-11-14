namespace Platform.Api.ViewModels;

public sealed record ClinicPortalBookingInspectViewModel(
    Guid ClinicId,
    ClinicPortalBookingSettingViewModel Settings,
    int PendingRequestsCount,
    int TodayPortalBookingsCount,
    int BlockedPatientsCount,
    IReadOnlyList<PortalBookingRequestItemViewModel> PendingRequests);

namespace Core.Application;

public sealed record ClinicPortalBookingInspectResult(
    Guid ClinicId,
    ClinicPortalBookingSettingResult Settings,
    int PendingRequestsCount,
    int TodayPortalBookingsCount,
    int BlockedPatientsCount,
    IReadOnlyList<PortalBookingRequestItemResult> PendingRequests);

namespace Platform.Api.ViewModels;

public sealed record PortalAvailabilityViewModel(
    Guid ClinicId,
    string ClinicTimeZone,
    string BookingMode,
    int CancellationCutoffHours,
    int MaxActiveFutureBookingsPerPatient,
    int ActiveFutureBookingsCount,
    bool IsPatientOnlineBookingBlocked,
    string? OnlineBookingBlockReason,
    IReadOnlyList<PortalBookableCategoryItemViewModel> Categories,
    IReadOnlyList<PortalBookableDoctorItemViewModel> Doctors,
    IReadOnlyList<PortalAvailableSlotItemViewModel> Slots);

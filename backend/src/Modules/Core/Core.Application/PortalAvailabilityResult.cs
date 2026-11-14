namespace Core.Application;

public sealed record PortalAvailabilityResult(
    Guid ClinicId,
    string ClinicTimeZone,
    string BookingMode,
    int CancellationCutoffHours,
    int MaxActiveFutureBookingsPerPatient,
    int ActiveFutureBookingsCount,
    bool IsPatientOnlineBookingBlocked,
    string? OnlineBookingBlockReason,
    IReadOnlyList<PortalBookableCategoryItemResult> Categories,
    IReadOnlyList<PortalBookableDoctorItemResult> Doctors,
    IReadOnlyList<PortalAvailableSlotItemResult> Slots);

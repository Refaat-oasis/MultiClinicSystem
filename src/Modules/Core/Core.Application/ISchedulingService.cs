using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Application service managing clinic scheduling settings, appointment categories, booking,
/// state transitions, day closure cascades, waiting queue, waiting list, and Super Admin inspection.
/// </summary>
public interface ISchedulingService
{
    Task<Result<ClinicSchedulingSettingResult>> GetSchedulingSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicSchedulingSettingResult>> UpdateSchedulingSettingsAsync(
        Guid userId,
        Guid clinicId,
        int vipJumpLimit,
        int lateGraceMinutes,
        int maxQueuePushes,
        int defaultSlotStepMinutes,
        int minBookingLeadMinutes,
        string? rescheduleOptionsJson,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AppointmentCategoryResult>>> ListCategoriesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentCategoryResult>> CreateCategoryAsync(
        Guid userId,
        Guid clinicId,
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        decimal defaultPriceAmount,
        string? defaultPriceCurrency,
        bool bookableOnline,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentCategoryResult>> UpdateCategoryAsync(
        Guid userId,
        Guid clinicId,
        Guid categoryId,
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        decimal defaultPriceAmount,
        string? defaultPriceCurrency,
        bool bookableOnline,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<DoctorBookingSettingResult>> UpdateDoctorBookingSettingAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        bool acceptsOnlineBooking,
        int? maxDailyOnlineBookings,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AppointmentItemResult>>> ListAppointmentsAsync(
        Guid userId,
        Guid clinicId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? doctorId,
        string? status,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> GetAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> BookAppointmentAsync(
        Guid userId,
        Guid clinicId,
        BookAppointmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> TransitionAppointmentStatusAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        string nextStatus,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> RescheduleAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        RescheduleAppointmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<CloseDayResult>> CloseDayAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string startDate,
        string endDate,
        string reasonAr,
        string reasonEn,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<DoctorTimetableResult>> GetDoctorTimetableAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        string date,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<QueueEntryItemResult>>> GetQueueBoardAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string? date,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<QueueEntryItemResult>> EnqueueWalkInAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        string? priority,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<QueueEntryItemResult>> PushLateQueueEntryAsync(
        Guid userId,
        Guid clinicId,
        Guid queueEntryId,
        bool markNoShowOnExceed,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<QueueEntryItemResult>> CallNextQueueEntryAsync(
        Guid userId,
        Guid clinicId,
        Guid queueEntryId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<WaitingListItemResult>> AddToWaitingListAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid? doctorId,
        Guid? categoryId,
        string preferredDate,
        string? notes,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WaitingListItemResult>>> ListWaitingListAsync(
        Guid userId,
        Guid clinicId,
        string? date,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SlotOfferItemResult>>> ListSlotOffersAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> AcceptSlotOfferAsync(
        Guid userId,
        Guid clinicId,
        Guid offerId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicScheduleInspectResult>> InspectScheduleAsSuperAdminAsync(
        Guid superAdminId,
        Guid clinicId,
        string? date,
        CancellationToken cancellationToken = default);
}


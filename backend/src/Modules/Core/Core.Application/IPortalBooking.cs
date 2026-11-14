using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Contract used by the Phase 11 patient portal to book and cancel the patient's own appointments
/// through the exact same validation pipeline as staff booking.
/// </summary>
public interface IPortalBooking
{
    Task<Result<AppointmentItemResult>> BookPortalAppointmentAsync(
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        Guid categoryId,
        DateTimeOffset startUtc,
        string? notes,
        Guid? operationId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> CancelPortalAppointmentAsync(
        Guid clinicId,
        Guid patientId,
        Guid appointmentId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> ApprovePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AppointmentItemResult>> DeclinePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> ExpireStalePortalBookingRequestsAsync(
        Guid clinicId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);
}


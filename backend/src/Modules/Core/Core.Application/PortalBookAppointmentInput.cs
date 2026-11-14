namespace Core.Application;

public sealed record PortalBookAppointmentInput(
    Guid DoctorId,
    Guid CategoryId,
    DateTimeOffset StartAtUtc,
    string? Notes,
    Guid? OperationId);

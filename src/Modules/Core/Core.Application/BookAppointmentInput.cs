namespace Core.Application;

/// <summary>
/// Input model for creating an appointment with optional client-generated GUID v7 ID and OperationId.
/// </summary>
public sealed record BookAppointmentInput(
    Guid? Id,
    Guid PatientId,
    Guid DoctorId,
    Guid? RoomId,
    Guid CategoryId,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Priority,
    Guid? PlanSessionId,
    string? Notes,
    Guid? OperationId);


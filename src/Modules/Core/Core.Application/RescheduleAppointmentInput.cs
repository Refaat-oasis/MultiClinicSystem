namespace Core.Application;

/// <summary>
/// Input model for rescheduling an appointment using an owner preset or custom interval.
/// </summary>
public sealed record RescheduleAppointmentInput(
    string? PresetKey,
    DateTimeOffset? CustomStartUtc,
    DateTimeOffset? CustomEndUtc,
    Guid? NewDoctorId,
    Guid? NewRoomId,
    Guid? OperationId);


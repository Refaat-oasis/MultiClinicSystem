namespace Core.Application;

/// <summary>
/// Input model to start or resume a clinical session for an appointment or walk-in patient.
/// </summary>
public sealed record StartVisitInput(
    Guid? ClientVisitId,
    Guid? AppointmentId,
    Guid? PatientId,
    Guid? DoctorId,
    string? SpecialtyCode,
    Guid? RoomId,
    int? PlannedDurationMinutes,
    bool IsVisibleToPatient,
    Guid? OperationId);


namespace Core.Application;

/// <summary>
/// Input model for toggling the average-time engine for a specific doctor.
/// </summary>
public sealed record SetDoctorTimeEngineInput(
    bool IsEngineEnabled,
    Guid? OperationId);


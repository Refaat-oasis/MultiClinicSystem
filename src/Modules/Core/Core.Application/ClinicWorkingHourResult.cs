namespace Core.Application;

/// <summary>
/// Read model for a clinic-wide or doctor-specific working hour slot.
/// </summary>
public sealed record ClinicWorkingHourResult(
    Guid Id,
    Guid ClinicId,
    Guid? DoctorId,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    bool IsClosed);

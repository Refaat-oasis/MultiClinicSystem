namespace Core.Domain;

/// <summary>
/// Input window describing weekly working hours for clinic baseline or doctor override.
/// </summary>
public sealed record AvailabilityWorkingHourWindow(
    Guid? DoctorId,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    bool IsClosed);


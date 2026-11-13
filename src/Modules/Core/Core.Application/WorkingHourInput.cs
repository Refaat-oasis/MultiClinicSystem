namespace Core.Application;

/// <summary>
/// Input model for setting a single day's working hours.
/// </summary>
public sealed record WorkingHourInput(
    int DayOfWeek,
    string StartTime,
    string EndTime,
    bool IsClosed);

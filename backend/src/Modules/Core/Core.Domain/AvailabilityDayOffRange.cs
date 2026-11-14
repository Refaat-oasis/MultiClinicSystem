namespace Core.Domain;

/// <summary>
/// Input date range representing a clinic-wide or doctor-specific closed day/holiday.
/// </summary>
public sealed record AvailabilityDayOffRange(
    Guid? DoctorId,
    string StartDate,
    string EndDate);


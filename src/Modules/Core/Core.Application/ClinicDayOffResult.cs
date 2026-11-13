namespace Core.Application;

/// <summary>
/// Read model for a scheduled clinic or doctor day off.
/// </summary>
public sealed record ClinicDayOffResult(
    Guid Id,
    Guid ClinicId,
    Guid? DoctorId,
    string StartDate,
    string EndDate,
    string ReasonAr,
    string ReasonEn,
    DateTimeOffset CreatedAtUtc);

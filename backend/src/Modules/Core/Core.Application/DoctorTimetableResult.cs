namespace Core.Application;

/// <summary>
/// Doctor daily timetable with summary counts by category and status.
/// </summary>
public sealed record DoctorTimetableResult(
    Guid DoctorId,
    string DoctorNameAr,
    string DoctorNameEn,
    string Date,
    int TotalCount,
    IReadOnlyDictionary<string, int> CountByCategory,
    IReadOnlyDictionary<string, int> CountByStatus,
    IReadOnlyList<AppointmentItemResult> Appointments);


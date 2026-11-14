namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a doctor's daily timetable summary.
/// </summary>
public sealed record DoctorTimetableViewModel(
    Guid DoctorId,
    string DoctorNameAr,
    string DoctorNameEn,
    string Date,
    int TotalCount,
    IReadOnlyDictionary<string, int> CountByCategory,
    IReadOnlyDictionary<string, int> CountByStatus,
    IReadOnlyList<AppointmentItemViewModel> Appointments);


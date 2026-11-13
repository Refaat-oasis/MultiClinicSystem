namespace Platform.Api.ViewModels;

public sealed record ClinicWorkingHourViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? DoctorId,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    bool IsClosed);

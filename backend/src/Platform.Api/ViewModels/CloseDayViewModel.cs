namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel listing a newly closed day range and affected appointments.
/// </summary>
public sealed record CloseDayViewModel(
    Guid DayOffId,
    string StartDate,
    string EndDate,
    IReadOnlyList<AppointmentItemViewModel> AffectedAppointments);


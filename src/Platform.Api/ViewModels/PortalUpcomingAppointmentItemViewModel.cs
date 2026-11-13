namespace Platform.Api.ViewModels;

public sealed record PortalUpcomingAppointmentItemViewModel(
    Guid AppointmentId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string? DoctorName,
    string SpecialtyCode,
    string CategoryNameAr,
    string CategoryNameEn,
    string Status);


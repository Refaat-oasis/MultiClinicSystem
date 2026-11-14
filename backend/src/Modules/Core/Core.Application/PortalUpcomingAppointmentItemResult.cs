namespace Core.Application;

public sealed record PortalUpcomingAppointmentItemResult(
    Guid AppointmentId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string? DoctorName,
    string SpecialtyCode,
    string CategoryNameAr,
    string CategoryNameEn,
    string Status);


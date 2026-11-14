namespace Core.Application;

public sealed record PortalBookingRequestItemResult(
    Guid AppointmentId,
    Guid ClinicId,
    Guid PatientId,
    string PatientFileNumber,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string Status,
    DateTimeOffset? ApprovalExpiresAtUtc,
    bool IsExpired,
    string? Notes,
    string? ReviewNotes,
    DateTimeOffset CreatedAtUtc);

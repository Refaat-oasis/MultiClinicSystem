namespace Core.Application;

/// <summary>
/// Detailed read model for an appointment with allowed transitions from its current status.
/// </summary>
public sealed record AppointmentItemResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    string PatientName,
    string PatientFileNumber,
    Guid DoctorId,
    string DoctorNameAr,
    string DoctorNameEn,
    string SpecialtyCode,
    Guid? RoomId,
    string? RoomCode,
    Guid CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Status,
    string Priority,
    string Source,
    int PushCount,
    Guid? PlanSessionId,
    string? Notes,
    string? CancelReason,
    IReadOnlyList<string> AllowedNextStatuses,
    DateTimeOffset CreatedAtUtc);


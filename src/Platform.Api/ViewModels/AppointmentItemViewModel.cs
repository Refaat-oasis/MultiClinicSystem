namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a booked clinic appointment.
/// </summary>
public sealed record AppointmentItemViewModel(
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


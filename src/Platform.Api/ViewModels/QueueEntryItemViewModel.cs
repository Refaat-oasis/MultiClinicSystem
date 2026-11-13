namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a waiting queue board entry.
/// </summary>
public sealed record QueueEntryItemViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? AppointmentId,
    Guid PatientId,
    string PatientName,
    string PatientFileNumber,
    Guid DoctorId,
    string DoctorNameAr,
    string QueueDate,
    string Priority,
    int OrderIndex,
    int OriginalPosition,
    int VipJumpCount,
    int PushCount,
    string Status,
    DateTimeOffset? ArrivedAtUtc,
    DateTimeOffset? CalledAtUtc,
    DateTimeOffset? ServedAtUtc);


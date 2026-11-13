namespace Core.Application;

/// <summary>
/// Read model for a live queue entry on the clinic reception/doctor queue board.
/// </summary>
public sealed record QueueEntryItemResult(
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


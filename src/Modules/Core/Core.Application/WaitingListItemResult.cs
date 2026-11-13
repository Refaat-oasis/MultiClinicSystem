namespace Core.Application;

/// <summary>
/// Read model for a patient waiting-list entry.
/// </summary>
public sealed record WaitingListItemResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    string PatientName,
    string PatientFileNumber,
    Guid? DoctorId,
    Guid? CategoryId,
    string PreferredDate,
    string? Notes,
    bool IsFulfilled,
    DateTimeOffset CreatedAtUtc);


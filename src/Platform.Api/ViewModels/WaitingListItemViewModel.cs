namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a waiting-list entry.
/// </summary>
public sealed record WaitingListItemViewModel(
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


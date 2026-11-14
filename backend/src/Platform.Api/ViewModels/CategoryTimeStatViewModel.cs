namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model comparing default, suggested, and applied durations per doctor and category.
/// </summary>
public sealed record CategoryTimeStatViewModel(
    Guid Id,
    Guid ClinicId,
    Guid DoctorId,
    string DoctorName,
    Guid CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    int SampleCount,
    int ExcusedCount,
    decimal TrimmedAverageMinutes,
    int SuggestedDurationMinutes,
    int AppliedDurationMinutes,
    int DefaultDurationMinutes,
    bool IsDoctorEngineEnabled,
    bool IsAdopted,
    DateTimeOffset LastUpdatedAtUtc);


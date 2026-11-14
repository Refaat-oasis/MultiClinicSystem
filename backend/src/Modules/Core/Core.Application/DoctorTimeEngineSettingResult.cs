namespace Core.Application;

/// <summary>
/// Read projection for a doctor's time-engine toggle setting.
/// </summary>
public sealed record DoctorTimeEngineSettingResult(
    Guid ClinicId,
    Guid DoctorId,
    string DoctorName,
    bool IsEngineEnabled,
    DateTimeOffset UpdatedAtUtc);


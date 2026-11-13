using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Application service managing the adaptive average-time engine, excused sessions,
/// per-doctor toggles, clinic thresholds, background recalculation, and Super Admin inspection.
/// </summary>
public interface ITimeEngineService
{
    Task<Result<ClinicTimeEngineSettingResult>> GetClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicTimeEngineSettingResult>> UpdateClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicTimeEngineSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DoctorTimeEngineSettingResult>>> ListDoctorSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<DoctorTimeEngineSettingResult>> SetDoctorEngineEnabledAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        SetDoctorTimeEngineInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CategoryTimeStatResult>>> ListCategoryStatsAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        Guid? categoryId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SessionDurationResult>>> ListSessionDurationsAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        Guid? categoryId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<SessionDurationResult>> SetSessionDurationExcusedAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        SetSessionDurationExcusedInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CategoryTimeStatResult>>> RecalculateClinicStatsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task ProcessCompletedSessionAsync(
        Guid clinicId,
        Guid visitId,
        Guid doctorId,
        Guid categoryId,
        int rawMinutes,
        int plannedMinutes,
        bool isExcused,
        string? excuseReason,
        DateTimeOffset occurredAtUtc,
        Guid? operationId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicTimeEngineInspectResult>> InspectClinicTimeEngineAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}


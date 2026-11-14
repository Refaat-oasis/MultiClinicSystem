using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Maps time-engine application results into API presentation view models.
/// </summary>
public static class TimeEngineViewModelMapper
{
    public static ClinicTimeEngineSettingViewModel ToViewModel(ClinicTimeEngineSettingResult r) => new(
        r.ClinicId,
        r.MinSampleSize,
        r.TrimFraction,
        r.MaxStepChangePercent,
        r.UpdatedAtUtc);

    public static DoctorTimeEngineSettingViewModel ToViewModel(DoctorTimeEngineSettingResult r) => new(
        r.ClinicId,
        r.DoctorId,
        r.DoctorName,
        r.IsEngineEnabled,
        r.UpdatedAtUtc);

    public static CategoryTimeStatViewModel ToViewModel(CategoryTimeStatResult r) => new(
        r.Id,
        r.ClinicId,
        r.DoctorId,
        r.DoctorName,
        r.CategoryId,
        r.CategoryNameAr,
        r.CategoryNameEn,
        r.SampleCount,
        r.ExcusedCount,
        r.TrimmedAverageMinutes,
        r.SuggestedDurationMinutes,
        r.AppliedDurationMinutes,
        r.DefaultDurationMinutes,
        r.IsDoctorEngineEnabled,
        r.IsAdopted,
        r.LastUpdatedAtUtc);

    public static SessionDurationViewModel ToViewModel(SessionDurationResult r) => new(
        r.Id,
        r.ClinicId,
        r.VisitId,
        r.DoctorId,
        r.CategoryId,
        r.RawMinutes,
        r.PlannedMinutes,
        r.IsExcused,
        r.ExcuseReason,
        r.OccurredAtUtc);

    public static ClinicTimeEngineInspectViewModel ToViewModel(ClinicTimeEngineInspectResult r) => new(
        r.ClinicId,
        ToViewModel(r.Settings),
        r.DoctorSettings.Select(ToViewModel).ToList(),
        r.Stats.Select(ToViewModel).ToList(),
        r.RecentDurations.Select(ToViewModel).ToList());
}


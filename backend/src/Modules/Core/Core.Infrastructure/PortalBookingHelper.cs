using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Shared internal helper for ClinicPortalBookingSetting resolution, no-show accounting, and portal booking mappers.
/// </summary>
internal static class PortalBookingHelper
{
    public static async Task<ClinicPortalBookingSetting> GetOrBuildDefaultBookingSettingsAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ClinicPortalBookingSetting? existing = await dbContext.ClinicPortalBookingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

        return existing ?? new ClinicPortalBookingSetting(GuidV7.NewGuid(), clinicId, nowUtc);
    }

    public static async Task RecordPatientNoShowAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        Guid patientId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        PatientAccount? account = await dbContext.PatientAccounts
            .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patientId && !a.IsVoided, cancellationToken);
        if (account == null)
        {
            return;
        }

        ClinicPortalBookingSetting bookingSetting = await GetOrBuildDefaultBookingSettingsAsync(
            dbContext, clinicId, nowUtc, cancellationToken);

        account.RecordNoShow(bookingSetting.AutoBlockAfterNoShows, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static ClinicPortalBookingSettingResult MapBookingSettings(ClinicPortalBookingSetting setting)
    {
        return new ClinicPortalBookingSettingResult(
            ClinicId: setting.ClinicId,
            Mode: setting.Mode.ToString(),
            MinLeadTimeMinutes: setting.MinLeadTimeMinutes,
            MaxDaysAhead: setting.MaxDaysAhead,
            SlotStepMinutes: setting.SlotStepMinutes,
            MaxActiveFutureBookingsPerPatient: setting.MaxActiveFutureBookingsPerPatient,
            MaxBookingsPerDay: setting.MaxBookingsPerDay,
            CancellationCutoffHours: setting.CancellationCutoffHours,
            AutoBlockAfterNoShows: setting.AutoBlockAfterNoShows,
            ApprovalHoldHours: setting.ApprovalHoldHours,
            ShowPricesWhenAvailable: setting.ShowPricesWhenAvailable,
            UpdatedAtUtc: setting.UpdatedAtUtc);
    }
}

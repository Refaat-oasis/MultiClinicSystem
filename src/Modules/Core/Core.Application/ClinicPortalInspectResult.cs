namespace Core.Application;

public sealed record ClinicPortalInspectResult(
    Guid ClinicId,
    int TotalAccountsCount,
    int ActiveAccountsCount,
    int InvitedAccountsCount,
    int DisabledAccountsCount,
    int PatientViewsLast30DaysCount,
    ClinicPortalSettingResult Settings);


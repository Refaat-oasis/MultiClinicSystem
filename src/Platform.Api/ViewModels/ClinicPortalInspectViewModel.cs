namespace Platform.Api.ViewModels;

public sealed record ClinicPortalInspectViewModel(
    Guid ClinicId,
    int TotalAccountsCount,
    int ActiveAccountsCount,
    int InvitedAccountsCount,
    int DisabledAccountsCount,
    int PatientViewsLast30DaysCount,
    ClinicPortalSettingViewModel Settings);


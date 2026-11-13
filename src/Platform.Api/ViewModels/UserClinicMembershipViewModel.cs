namespace Platform.Api.ViewModels;

public sealed record UserClinicMembershipViewModel(
    Guid ClinicId,
    string ClinicName,
    string Slug,
    string Role,
    string Status,
    string OnboardingStatus);


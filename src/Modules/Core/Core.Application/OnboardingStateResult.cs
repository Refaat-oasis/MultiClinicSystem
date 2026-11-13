namespace Core.Application;

/// <summary>
/// Full resumable state of a clinic's onboarding wizard.
/// </summary>
public sealed record OnboardingStateResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string OnboardingStatus,
    bool WelcomeSeen,
    IReadOnlyList<string> CompletedSteps,
    string? Street,
    string? City,
    string? Governorate,
    string? MainPhone,
    IReadOnlyList<string> ExtraPhones,
    string? ContactEmail,
    Guid? LogoAssetId,
    IReadOnlyList<ClinicSpecialtyItemResult> Specialties);


using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Application service managing clinic creation, quotas, switching, resumable onboarding, settings, and public slug resolution.
/// </summary>
public interface IClinicManagementService
{
    Task<Result<IReadOnlyList<MyClinicCardResult>>> ListMyClinicsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<MyClinicCardResult>> CreateClinicAsync(
        Guid userId,
        string name,
        string? timeZone,
        string? currency,
        string? roundingRule,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SwitchClinicResult>> SwitchClinicAsync(
        Guid userId,
        Guid clinicId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SlugCheckResult>> CheckSlugAsync(
        string? rawSlug,
        Guid? clinicId,
        CancellationToken cancellationToken = default);

    IReadOnlyList<SpecialtyCatalogItemResult> GetSpecialtyCatalog();

    Task<Result<OnboardingStateResult>> GetOnboardingStateAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingStateResult>> CompleteWelcomeStepAsync(
        Guid userId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingStateResult>> SaveOnboardingDetailsAsync(
        Guid userId,
        Guid clinicId,
        string name,
        string street,
        string city,
        string governorate,
        string mainPhone,
        IReadOnlyList<string>? extraPhones,
        string? contactEmail,
        Guid? logoAssetId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<LogoUploadResult>> UploadClinicLogoAsync(
        Guid userId,
        Guid clinicId,
        string fileName,
        string contentType,
        byte[] rawBytes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingStateResult>> SaveOnboardingTypesAsync(
        Guid userId,
        Guid clinicId,
        IReadOnlyList<SpecialtySelectionInput> selections,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingStateResult>> SaveOnboardingSlugAsync(
        Guid userId,
        Guid clinicId,
        string slug,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicDashboardResult>> CompleteOnboardingAsync(
        Guid userId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicDashboardResult>> GetClinicDashboardAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicSettingsResult>> GetClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicSettingsResult>> UpdateClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        string name,
        string street,
        string city,
        string governorate,
        string mainPhone,
        IReadOnlyList<string>? extraPhones,
        string? contactEmail,
        string? slug,
        string? timeZone,
        string? currency,
        string? roundingRule,
        Guid? logoAssetId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PublicClinicResolutionResult>> ResolvePublicSlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}


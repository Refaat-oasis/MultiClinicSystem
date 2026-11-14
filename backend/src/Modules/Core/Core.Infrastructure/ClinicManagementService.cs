using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Manages clinic creation within owner quotas, multi-clinic switching, resumable onboarding wizard,
/// public slug resolution/redirects, general clinic settings, and logo upload sanitization.
/// </summary>
public sealed class ClinicManagementService : IClinicManagementService
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly ITokenService _tokenService;
    private readonly IFileStorage _fileStorage;
    private readonly ISpecialtyCatalog _specialtyCatalog;
    private readonly ISettingsSectionRegistry _settingsSectionRegistry;
    private readonly IOutbox _outbox;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ClinicManagementService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        CurrentClinicContext currentClinicContext,
        ITokenService tokenService,
        IFileStorage fileStorage,
        ISpecialtyCatalog specialtyCatalog,
        ISettingsSectionRegistry settingsSectionRegistry,
        IOutbox outbox,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _specialtyCatalog = specialtyCatalog ?? throw new ArgumentNullException(nameof(specialtyCatalog));
        _settingsSectionRegistry = settingsSectionRegistry ?? throw new ArgumentNullException(nameof(settingsSectionRegistry));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<MyClinicCardResult>>> ListMyClinicsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);

        var memberClinicIds = await _dbContext.UserClinics
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.ClinicId)
            .ToListAsync(cancellationToken);

        IQueryable<Clinic> query = _dbContext.Clinics;
        if (owner != null)
        {
            query = query.Where(c => c.OwnerId == owner.Id || memberClinicIds.Contains(c.Id));
        }
        else
        {
            query = query.Where(c => memberClinicIds.Contains(c.Id));
        }

        var clinics = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var cards = new List<MyClinicCardResult>(clinics.Count);
        foreach (var clinic in clinics)
        {
            var (steps, enabledModules) = await ExecuteForClinicAsync(clinic.Id, async () =>
            {
                var stepList = await _dbContext.OnboardingProgresses
                    .Where(p => p.ClinicId == clinic.Id)
                    .OrderBy(p => p.CompletedAtUtc)
                    .Select(p => p.StepKey)
                    .ToListAsync(cancellationToken);

                var moduleList = await _dbContext.ClinicModules
                    .Where(m => m.ClinicId == clinic.Id && m.Enabled)
                    .OrderBy(m => m.ModuleKey)
                    .Select(m => m.ModuleKey)
                    .ToListAsync(cancellationToken);

                return (stepList, moduleList);
            });

            cards.Add(new MyClinicCardResult(
                ClinicId: clinic.Id,
                Name: clinic.Name,
                Slug: clinic.Slug,
                Status: clinic.Status,
                OnboardingStatus: clinic.OnboardingStatus,
                TimeZone: clinic.TimeZone,
                Currency: clinic.Currency,
                RoundingRule: clinic.RoundingRule,
                LogoAssetId: clinic.LogoAssetId,
                ModulesEnabled: enabledModules,
                CompletedSteps: steps,
                WelcomeSeen: owner?.WelcomeSeen ?? false));
        }

        return Result<IReadOnlyList<MyClinicCardResult>>.Success(cards);
    }

    public async Task<Result<MyClinicCardResult>> CreateClinicAsync(
        Guid userId,
        string name,
        string? timeZone,
        string? currency,
        string? roundingRule,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<MyClinicCardResult>.Failure(Error.Validation("CLINIC_NAME_REQUIRED", "Clinic name is required."));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive)
        {
            return Result<MyClinicCardResult>.Failure(Error.Unauthorized("UNAUTHORIZED", "Authenticated clinic account not found."));
        }

        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);

        if (owner == null)
        {
            return Result<MyClinicCardResult>.Failure(Error.Forbidden("FORBIDDEN_OWNER_ONLY", "Only clinic owners can create new clinics."));
        }

        if (!string.Equals(owner.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result<MyClinicCardResult>.Failure(Error.Forbidden("OWNER_DEACTIVATED", "Owner account is deactivated."));
        }

        if (owner.MaxClinics.HasValue)
        {
            int currentCount = await _dbContext.Clinics
                .CountAsync(c => c.OwnerId == owner.Id, cancellationToken);

            if (currentCount >= owner.MaxClinics.Value)
            {
                return Result<MyClinicCardResult>.Failure(Error.Conflict(
                    "CLINIC_QUOTA_REACHED",
                    $"Maximum clinic quota ({owner.MaxClinics.Value}) has been reached."));
            }
        }

        Guid clinicId = GuidV7.NewGuid();
        string initialSlug = await GenerateUniqueTemporarySlugAsync(name, clinicId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        var clinic = new Clinic(
            id: clinicId,
            name: name.Trim(),
            slug: initialSlug,
            createdAtUtc: now,
            ownerId: owner.Id,
            status: "PendingOnboarding",
            onboardingStatus: "NotStarted",
            timeZone: timeZone ?? "Africa/Cairo",
            currency: currency ?? "EGP",
            roundingRule: roundingRule ?? "NearestPound");

        _dbContext.Clinics.Add(clinic);
        _dbContext.ClinicSlugs.Add(new ClinicSlugHistory(GuidV7.NewGuid(), clinicId, initialSlug, isCurrent: true, now));
        _dbContext.UserClinics.Add(new UserClinic(GuidV7.NewGuid(), userId, clinicId, "Owner", now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _outbox.EnqueueAsync(
            "ClinicCreated",
            JsonSerializer.Serialize(new { clinicId = clinic.Id, ownerId = owner.Id, name = clinic.Name, status = clinic.Status }),
            cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicCreated",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { clinic.Name, clinic.Slug, clinic.Status, clinic.OnboardingStatus }),
            userId: userId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "web-browser" : deviceId.Trim();
        var tokenResult = await _tokenService.GenerateTokensAsync(user, cleanDeviceId, clinic.Id, cancellationToken);

        return Result<MyClinicCardResult>.Success(new MyClinicCardResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Status: clinic.Status,
            OnboardingStatus: clinic.OnboardingStatus,
            TimeZone: clinic.TimeZone,
            Currency: clinic.Currency,
            RoundingRule: clinic.RoundingRule,
            LogoAssetId: clinic.LogoAssetId,
            ModulesEnabled: Array.Empty<string>(),
            CompletedSteps: Array.Empty<string>(),
            WelcomeSeen: owner.WelcomeSeen,
            AccessToken: tokenResult.IsSuccess ? tokenResult.Value.AccessToken : null,
            RefreshToken: tokenResult.IsSuccess ? tokenResult.Value.RefreshToken : null,
            ExpiresAtUtc: tokenResult.IsSuccess ? tokenResult.Value.ExpiresAtUtc : null));
    }

    public async Task<Result<SwitchClinicResult>> SwitchClinicAsync(
        Guid userId,
        Guid clinicId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: false, cancellationToken);
        if (access.IsFailure)
        {
            return Result<SwitchClinicResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        if (!clinic.IsActive || string.Equals(clinic.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            return Result<SwitchClinicResult>.Failure(Error.Forbidden("CLINIC_SUSPENDED", "This clinic is currently suspended."));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<SwitchClinicResult>.Failure(Error.Unauthorized("UNAUTHORIZED", "Authenticated user not found."));
        }

        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "web-browser" : deviceId.Trim();
        var tokenResult = await _tokenService.GenerateTokensAsync(user, cleanDeviceId, clinic.Id, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<SwitchClinicResult>.Failure(tokenResult.Error);
        }

        await _auditService.LogAsync(
            action: "ClinicSwitched",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { clinic.Name, deviceId = cleanDeviceId }),
            userId: userId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<SwitchClinicResult>.Success(new SwitchClinicResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Status: clinic.Status,
            OnboardingStatus: clinic.OnboardingStatus,
            AccessToken: tokenResult.Value.AccessToken,
            RefreshToken: tokenResult.Value.RefreshToken,
            ExpiresAtUtc: tokenResult.Value.ExpiresAtUtc));
    }

    public async Task<Result<SlugCheckResult>> CheckSlugAsync(
        string? rawSlug,
        Guid? clinicId,
        CancellationToken cancellationToken = default)
    {
        var validation = SlugGenerator.Validate(rawSlug);
        if (validation.IsFailure)
        {
            string fallbackSuggestion = SlugGenerator.SuggestFromName(rawSlug, Guid.NewGuid().ToString("N")[..4]);
            return Result<SlugCheckResult>.Success(new SlugCheckResult(
                Slug: rawSlug?.Trim().ToLowerInvariant() ?? string.Empty,
                IsValid: false,
                IsAvailable: false,
                ErrorCode: validation.Error.Code,
                Reason: validation.Error.Message,
                SuggestedAlternative: fallbackSuggestion,
                PreviewUrl: $"https://clinic.local/c/{fallbackSuggestion}"));
        }

        string candidate = validation.Value;
        bool taken = await _dbContext.ClinicSlugs
            .AnyAsync(s => s.Slug == candidate && (!clinicId.HasValue || s.ClinicId != clinicId.Value), cancellationToken);

        string suggestion = candidate;
        if (taken)
        {
            suggestion = await GenerateUniqueTemporarySlugAsync(candidate, clinicId ?? Guid.NewGuid(), cancellationToken);
        }

        return Result<SlugCheckResult>.Success(new SlugCheckResult(
            Slug: candidate,
            IsValid: true,
            IsAvailable: !taken,
            ErrorCode: taken ? "SLUG_TAKEN" : null,
            Reason: taken ? "This public link is already claimed by another clinic." : null,
            SuggestedAlternative: suggestion,
            PreviewUrl: $"https://clinic.local/c/{candidate}"));
    }

    public IReadOnlyList<SpecialtyCatalogItemResult> GetSpecialtyCatalog()
    {
        return _specialtyCatalog.GetAll();
    }

    public async Task<Result<OnboardingStateResult>> GetOnboardingStateAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: false, cancellationToken);
        if (access.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(access.Error);
        }

        return Result<OnboardingStateResult>.Success(await BuildOnboardingStateAsync(access.Value.Clinic, access.Value.Owner, cancellationToken));
    }

    public async Task<Result<OnboardingStateResult>> CompleteWelcomeStepAsync(
        Guid userId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        clinic.StartOnboarding();
        access.Value.Owner?.MarkWelcomeSeen();

        await UpsertProgressStepAndSaveAsync(clinic.Id, "welcome", userId, cancellationToken);

        return Result<OnboardingStateResult>.Success(await BuildOnboardingStateAsync(clinic, access.Value.Owner, cancellationToken));
    }

    public async Task<Result<OnboardingStateResult>> SaveOnboardingDetailsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(access.Error);
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(governorate))
        {
            return Result<OnboardingStateResult>.Failure(Error.Validation(
                "CLINIC_DETAILS_REQUIRED",
                "Clinic name, street, city, and governorate are required."));
        }

        var mainPhoneNorm = PhoneNormalizer.Normalize(mainPhone);
        if (mainPhoneNorm.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(mainPhoneNorm.Error);
        }

        var normalizedExtras = new List<string>();
        if (extraPhones != null)
        {
            foreach (string rawExtra in extraPhones.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var extraNorm = PhoneNormalizer.Normalize(rawExtra);
                if (extraNorm.IsFailure)
                {
                    return Result<OnboardingStateResult>.Failure(extraNorm.Error);
                }

                normalizedExtras.Add(extraNorm.Value);
            }
        }

        var clinic = access.Value.Clinic;
        clinic.UpdateOnboardingDetails(
            name: name,
            street: street,
            city: city,
            governorate: governorate,
            mainPhone: mainPhoneNorm.Value,
            extraPhonesJson: JsonSerializer.Serialize(normalizedExtras),
            contactEmail: contactEmail,
            logoAssetId: logoAssetId);

        await UpsertProgressStepAndSaveAsync(clinic.Id, "details", userId, cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicOnboardingDetailsSaved",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { clinic.Name, clinic.City, clinic.Governorate, clinic.MainPhone }),
            userId: userId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<OnboardingStateResult>.Success(await BuildOnboardingStateAsync(clinic, access.Value.Owner, cancellationToken));
    }

    public async Task<Result<LogoUploadResult>> UploadClinicLogoAsync(
        Guid userId,
        Guid clinicId,
        string fileName,
        string contentType,
        byte[] rawBytes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<LogoUploadResult>.Failure(access.Error);
        }

        var sanitized = LogoImageProcessor.ProcessAndSanitize(rawBytes, contentType);
        if (sanitized.IsFailure)
        {
            return Result<LogoUploadResult>.Failure(sanitized.Error);
        }

        Guid assetId = GuidV7.NewGuid();
        string ext = sanitized.Value.ContentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            _ => "bin"
        };

        string fullKey = $"clinics/{clinicId:N}/logos/{assetId:N}-full.{ext}";
        string thumbKey = $"clinics/{clinicId:N}/logos/{assetId:N}-thumb.{ext}";

        await _fileStorage.SaveAsync(fullKey, sanitized.Value.FullBytes, sanitized.Value.ContentType, cancellationToken);
        await _fileStorage.SaveAsync(thumbKey, sanitized.Value.ThumbBytes, sanitized.Value.ContentType, cancellationToken);

        var clinic = access.Value.Clinic;
        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var asset = new ClinicAsset(
                id: assetId,
                clinicId: clinicId,
                ownerType: "ClinicLogo",
                fileName: string.IsNullOrWhiteSpace(fileName) ? $"logo.{ext}" : fileName.Trim(),
                contentType: sanitized.Value.ContentType,
                originalSizeBytes: sanitized.Value.FullBytes.LongLength,
                fullStorageKey: fullKey,
                thumbStorageKey: thumbKey,
                uploadedByUserId: userId,
                createdAtUtc: _clock.UtcNow);

            _dbContext.ClinicAssets.Add(asset);
            clinic.SetLogoAsset(assetId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await _auditService.LogAsync(
            action: "ClinicLogoUploaded",
            entityName: "ClinicAsset",
            entityId: assetId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { sanitized.Value.ContentType, size = sanitized.Value.FullBytes.LongLength }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<LogoUploadResult>.Success(new LogoUploadResult(
            AssetId: assetId,
            ContentType: sanitized.Value.ContentType,
            SizeBytes: sanitized.Value.FullBytes.LongLength,
            FullStorageKey: fullKey,
            ThumbStorageKey: thumbKey));
    }

    public async Task<Result<OnboardingStateResult>> SaveOnboardingTypesAsync(
        Guid userId,
        Guid clinicId,
        IReadOnlyList<SpecialtySelectionInput> selections,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(access.Error);
        }

        if (selections == null || selections.Count == 0)
        {
            return Result<OnboardingStateResult>.Failure(Error.Validation(
                "SPECIALTY_REQUIRED",
                "At least one clinic type/specialty must be selected."));
        }

        var clinic = access.Value.Clinic;
        clinic.StartOnboarding();

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var existing = await _dbContext.ClinicSpecialties
                .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                .ToListAsync(cancellationToken);

            foreach (var old in existing)
            {
                old.Void(_clock.UtcNow, userId.ToString(), "Replaced during onboarding");
            }

            foreach (var selection in selections)
            {
                var catalogItem = _specialtyCatalog.FindByCode(selection.CatalogCode);
                if (catalogItem == null)
                {
                    catalogItem = _specialtyCatalog.FindByCode("other")!;
                }

                bool isOther = string.Equals(catalogItem.CatalogCode, "other", StringComparison.OrdinalIgnoreCase);
                string nameAr = isOther && !string.IsNullOrWhiteSpace(selection.CustomNameAr)
                    ? selection.CustomNameAr.Trim()
                    : catalogItem.NameAr;
                string nameEn = isOther && !string.IsNullOrWhiteSpace(selection.CustomNameEn)
                    ? selection.CustomNameEn.Trim()
                    : catalogItem.NameEn;

                _dbContext.ClinicSpecialties.Add(new ClinicSpecialty(
                    id: GuidV7.NewGuid(),
                    clinicId: clinicId,
                    catalogCode: catalogItem.CatalogCode,
                    nameAr: nameAr,
                    nameEn: nameEn,
                    defaultFormSchemaJson: catalogItem.DefaultFormSchemaJson,
                    defaultAppointmentCategoriesJson: catalogItem.DefaultAppointmentCategoriesJson,
                    hasDentalPlugin: catalogItem.HasDentalPlugin,
                    createdAtUtc: _clock.UtcNow));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await UpsertProgressStepAndSaveAsync(clinicId, "types", userId, cancellationToken);

        return Result<OnboardingStateResult>.Success(await BuildOnboardingStateAsync(clinic, access.Value.Owner, cancellationToken));
    }

    public async Task<Result<OnboardingStateResult>> SaveOnboardingSlugAsync(
        Guid userId,
        Guid clinicId,
        string slug,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        var slugUpdateResult = await AssignSlugAsync(clinic, slug, cancellationToken);
        if (slugUpdateResult.IsFailure)
        {
            return Result<OnboardingStateResult>.Failure(slugUpdateResult.Error);
        }

        clinic.StartOnboarding();
        await UpsertProgressStepAndSaveAsync(clinicId, "slug", userId, cancellationToken);

        return Result<OnboardingStateResult>.Success(await BuildOnboardingStateAsync(clinic, access.Value.Owner, cancellationToken));
    }

    public async Task<Result<ClinicDashboardResult>> CompleteOnboardingAsync(
        Guid userId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicDashboardResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        var completeResult = clinic.CompleteOnboarding();
        if (completeResult.IsFailure)
        {
            return Result<ClinicDashboardResult>.Failure(completeResult.Error);
        }

        await UpsertProgressStepAndSaveAsync(clinicId, "review", userId, cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicOnboardingCompleted",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { clinic.Name, clinic.Slug, clinic.OnboardingStatus, clinic.Status }),
            userId: userId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return await BuildDashboardAsync(clinic, cancellationToken);
    }

    public async Task<Result<ClinicDashboardResult>> GetClinicDashboardAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: false, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicDashboardResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        if (!string.Equals(clinic.OnboardingStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClinicDashboardResult>.Failure(Error.Conflict(
                "ONBOARDING_REQUIRED",
                "Clinic onboarding must be completed before accessing clinic operations."));
        }

        return await BuildDashboardAsync(clinic, cancellationToken);
    }

    public async Task<Result<ClinicSettingsResult>> GetClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: false, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicSettingsResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        if (!string.Equals(clinic.OnboardingStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClinicSettingsResult>.Failure(Error.Conflict(
                "ONBOARDING_REQUIRED",
                "Clinic onboarding must be completed before accessing clinic settings."));
        }

        return Result<ClinicSettingsResult>.Success(await BuildSettingsResultAsync(clinic, cancellationToken));
    }

    public async Task<Result<ClinicSettingsResult>> UpdateClinicSettingsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAuthorizedClinicAsync(userId, clinicId, requireOwner: true, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicSettingsResult>.Failure(access.Error);
        }

        var clinic = access.Value.Clinic;
        if (!string.Equals(clinic.OnboardingStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClinicSettingsResult>.Failure(Error.Conflict(
                "ONBOARDING_REQUIRED",
                "Clinic onboarding must be completed before editing settings."));
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(governorate))
        {
            return Result<ClinicSettingsResult>.Failure(Error.Validation(
                "CLINIC_DETAILS_REQUIRED",
                "Clinic name, street, city, and governorate are required."));
        }

        var mainPhoneNorm = PhoneNormalizer.Normalize(mainPhone);
        if (mainPhoneNorm.IsFailure)
        {
            return Result<ClinicSettingsResult>.Failure(mainPhoneNorm.Error);
        }

        var normalizedExtras = new List<string>();
        if (extraPhones != null)
        {
            foreach (string rawExtra in extraPhones.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var extraNorm = PhoneNormalizer.Normalize(rawExtra);
                if (extraNorm.IsFailure)
                {
                    return Result<ClinicSettingsResult>.Failure(extraNorm.Error);
                }

                normalizedExtras.Add(extraNorm.Value);
            }
        }

        string oldSnapshot = JsonSerializer.Serialize(new
        {
            clinic.Name,
            clinic.Slug,
            clinic.Street,
            clinic.City,
            clinic.Governorate,
            clinic.MainPhone,
            clinic.TimeZone,
            clinic.Currency,
            clinic.RoundingRule
        });

        var updateResult = clinic.UpdateGeneralSettings(
            name: name,
            street: street,
            city: city,
            governorate: governorate,
            mainPhone: mainPhoneNorm.Value,
            extraPhonesJson: JsonSerializer.Serialize(normalizedExtras),
            contactEmail: contactEmail,
            timeZone: timeZone,
            currency: currency,
            roundingRule: roundingRule,
            logoAssetId: logoAssetId);

        if (updateResult.IsFailure)
        {
            return Result<ClinicSettingsResult>.Failure(updateResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(slug) &&
            !string.Equals(slug.Trim(), clinic.Slug, StringComparison.OrdinalIgnoreCase))
        {
            var slugAssign = await AssignSlugAsync(clinic, slug, cancellationToken);
            if (slugAssign.IsFailure)
            {
                return Result<ClinicSettingsResult>.Failure(slugAssign.Error);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        string newSnapshot = JsonSerializer.Serialize(new
        {
            clinic.Name,
            clinic.Slug,
            clinic.Street,
            clinic.City,
            clinic.Governorate,
            clinic.MainPhone,
            clinic.TimeZone,
            clinic.Currency,
            clinic.RoundingRule
        });

        await _auditService.LogAsync(
            action: "ClinicSettingsUpdated",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: newSnapshot,
            userId: userId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<ClinicSettingsResult>.Success(await BuildSettingsResultAsync(clinic, cancellationToken));
    }

    public async Task<Result<PublicClinicResolutionResult>> ResolvePublicSlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result<PublicClinicResolutionResult>.Failure(Error.NotFound("SLUG_NOT_FOUND", "Clinic link not found."));
        }

        string normalized = slug.Trim().ToLowerInvariant();
        var historyEntry = await _dbContext.ClinicSlugs
            .FirstOrDefaultAsync(s => s.Slug == normalized, cancellationToken);

        Clinic? clinic = null;
        if (historyEntry != null)
        {
            clinic = await _dbContext.Clinics
                .FirstOrDefaultAsync(c => c.Id == historyEntry.ClinicId, cancellationToken);
        }
        else
        {
            clinic = await _dbContext.Clinics
                .FirstOrDefaultAsync(c => c.Slug == normalized, cancellationToken);
        }

        if (clinic == null || !clinic.IsActive)
        {
            return Result<PublicClinicResolutionResult>.Failure(Error.NotFound("SLUG_NOT_FOUND", "Clinic link not found."));
        }

        bool isRedirect = !string.Equals(normalized, clinic.Slug, StringComparison.OrdinalIgnoreCase);
        return Result<PublicClinicResolutionResult>.Success(new PublicClinicResolutionResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            CurrentSlug: clinic.Slug,
            RequestedSlug: normalized,
            IsRedirect: isRedirect,
            RedirectUrl: $"/c/{clinic.Slug}",
            LogoAssetId: clinic.LogoAssetId));
    }

    private async Task<Result<(Clinic Clinic, Owner? Owner)>> ResolveAuthorizedClinicAsync(
        Guid userId,
        Guid clinicId,
        bool requireOwner,
        CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);

        if (clinic == null)
        {
            return Result<(Clinic, Owner?)>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);

        bool isOwner = owner != null && clinic.OwnerId == owner.Id;
        bool isMember = await _dbContext.UserClinics
            .AnyAsync(uc => uc.UserId == userId && uc.ClinicId == clinicId, cancellationToken);

        if (!isOwner && !isMember)
        {
            // Cross-clinic isolation: return 404, not 403
            return Result<(Clinic, Owner?)>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        if (requireOwner && !isOwner)
        {
            return Result<(Clinic, Owner?)>.Failure(Error.Forbidden(
                "FORBIDDEN_OWNER_ONLY",
                "Only the clinic owner can perform this action."));
        }

        return Result<(Clinic, Owner?)>.Success((clinic, owner));
    }

    private async Task<Result> AssignSlugAsync(
        Clinic clinic,
        string rawSlug,
        CancellationToken cancellationToken)
    {
        var validation = SlugGenerator.Validate(rawSlug);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error);
        }

        string candidate = validation.Value;
        if (string.Equals(candidate, clinic.Slug, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        bool usedAnywhere = await _dbContext.ClinicSlugs
            .AnyAsync(s => s.Slug == candidate, cancellationToken);

        if (usedAnywhere)
        {
            return Result.Failure(Error.Conflict("SLUG_TAKEN", "This public slug is already in use."));
        }

        DateTimeOffset now = _clock.UtcNow;
        var currentEntries = await _dbContext.ClinicSlugs
            .Where(s => s.ClinicId == clinic.Id && s.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var entry in currentEntries)
        {
            entry.MarkReplaced(now);
        }

        _dbContext.ClinicSlugs.Add(new ClinicSlugHistory(GuidV7.NewGuid(), clinic.Id, candidate, isCurrent: true, now));
        clinic.UpdateSlug(candidate);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return Result.Failure(Error.Conflict("SLUG_TAKEN", "This public slug is already in use."));
        }
    }

    private async Task<string> GenerateUniqueTemporarySlugAsync(
        string name,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        string suffix = clinicId.ToString("N")[..4];
        string baseSlug = SlugGenerator.SuggestFromName(name, suffix);

        if (!await _dbContext.ClinicSlugs.AnyAsync(s => s.Slug == baseSlug, cancellationToken))
        {
            return baseSlug;
        }

        for (int attempt = 1; attempt <= 20; attempt++)
        {
            string trimmedBase = baseSlug.Length > 34 ? baseSlug[..34].TrimEnd('-') : baseSlug;
            string candidate = $"{trimmedBase}-{suffix}-{attempt}";
            if (candidate.Length > 40)
            {
                candidate = candidate[..40].TrimEnd('-');
            }

            if (!await _dbContext.ClinicSlugs.AnyAsync(s => s.Slug == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return $"clinic-{Guid.NewGuid().ToString("N")[..8]}";
    }

    private async Task UpsertProgressStepAndSaveAsync(
        Guid clinicId,
        string stepKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var existing = await _dbContext.OnboardingProgresses
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.StepKey == stepKey, cancellationToken);

            if (existing == null)
            {
                _dbContext.OnboardingProgresses.Add(new OnboardingProgress(
                    GuidV7.NewGuid(),
                    clinicId,
                    stepKey,
                    _clock.UtcNow,
                    userId));
            }
            else
            {
                existing.MarkCompletedAgain(_clock.UtcNow, userId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });
    }

    private async Task<OnboardingStateResult> BuildOnboardingStateAsync(
        Clinic clinic,
        Owner? owner,
        CancellationToken cancellationToken)
    {
        var steps = await ExecuteForClinicAsync(clinic.Id, () =>
            _dbContext.OnboardingProgresses
                .Where(p => p.ClinicId == clinic.Id)
                .OrderBy(p => p.CompletedAtUtc)
                .Select(p => p.StepKey)
                .ToListAsync(cancellationToken));

        var specialties = await ExecuteForClinicAsync(clinic.Id, () =>
            _dbContext.ClinicSpecialties
                .Where(s => s.ClinicId == clinic.Id && !s.IsVoided)
                .OrderBy(s => s.CreatedAtUtc)
                .Select(s => new ClinicSpecialtyItemResult(
                    s.Id,
                    s.CatalogCode,
                    s.NameAr,
                    s.NameEn,
                    s.HasDentalPlugin,
                    s.DefaultFormSchemaJson,
                    s.DefaultAppointmentCategoriesJson))
                .ToListAsync(cancellationToken));

        var extraPhones = DeserializePhones(clinic.ExtraPhonesJson);

        return new OnboardingStateResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            OnboardingStatus: clinic.OnboardingStatus,
            WelcomeSeen: owner?.WelcomeSeen ?? false,
            CompletedSteps: steps,
            Street: clinic.Street,
            City: clinic.City,
            Governorate: clinic.Governorate,
            MainPhone: clinic.MainPhone,
            ExtraPhones: extraPhones,
            ContactEmail: clinic.ContactEmail,
            LogoAssetId: clinic.LogoAssetId,
            Specialties: specialties);
    }

    private async Task<Result<ClinicDashboardResult>> BuildDashboardAsync(
        Clinic clinic,
        CancellationToken cancellationToken)
    {
        var (specialtiesCount, modulesEnabled) = await ExecuteForClinicAsync(clinic.Id, async () =>
        {
            int specs = await _dbContext.ClinicSpecialties.CountAsync(s => s.ClinicId == clinic.Id && !s.IsVoided, cancellationToken);
            var mods = await _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinic.Id && m.Enabled)
                .OrderBy(m => m.ModuleKey)
                .Select(m => m.ModuleKey)
                .ToArrayAsync(cancellationToken);
            return (specs, mods);
        });

        bool awaitingSetup = modulesEnabled.Length == 0;

        return Result<ClinicDashboardResult>.Success(new ClinicDashboardResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Status: clinic.Status,
            OnboardingStatus: clinic.OnboardingStatus,
            ModulesEnabled: modulesEnabled,
            AwaitingModuleSetup: awaitingSetup,
            StatusMessageAr: awaitingSetup
                ? "بانتظار قيام مشرف المنصة بتفعيل الميزات والوحدات الخاصة بعيادتك"
                : "جميع الوحدات المفعلة جاهزة للعمل",
            StatusMessageEn: awaitingSetup
                ? "Waiting for the platform admin to enable your features"
                : "Enabled modules are active",
            OwnerChecklist:
            [
                "إعداد الأدوار والصلاحيات / Configure roles and permissions",
                "دعوة أعضاء الفريق والأطباء / Invite staff members and doctors",
                "تجهيز الغرف والعيادات الداخلية / Prepare rooms and internal clinics"
            ],
            SpecialtiesCount: specialtiesCount));
    }

    private async Task<ClinicSettingsResult> BuildSettingsResultAsync(
        Clinic clinic,
        CancellationToken cancellationToken)
    {
        var (specialties, enabledModules) = await ExecuteForClinicAsync(clinic.Id, async () =>
        {
            var specs = await _dbContext.ClinicSpecialties
                .Where(s => s.ClinicId == clinic.Id && !s.IsVoided)
                .OrderBy(s => s.CreatedAtUtc)
                .Select(s => new ClinicSpecialtyItemResult(
                    s.Id,
                    s.CatalogCode,
                    s.NameAr,
                    s.NameEn,
                    s.HasDentalPlugin,
                    s.DefaultFormSchemaJson,
                    s.DefaultAppointmentCategoriesJson))
                .ToListAsync(cancellationToken);

            var mods = await _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinic.Id && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);

            return (specs, mods);
        });

        var sections = _settingsSectionRegistry.GetAvailableSections(enabledModules);

        return new ClinicSettingsResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Street: clinic.Street ?? string.Empty,
            City: clinic.City ?? string.Empty,
            Governorate: clinic.Governorate ?? string.Empty,
            MainPhone: clinic.MainPhone ?? string.Empty,
            ExtraPhones: DeserializePhones(clinic.ExtraPhonesJson),
            ContactEmail: clinic.ContactEmail,
            TimeZone: clinic.TimeZone,
            Currency: clinic.Currency,
            RoundingRule: clinic.RoundingRule,
            HasFinancialEntries: clinic.HasFinancialEntries,
            LogoAssetId: clinic.LogoAssetId,
            Specialties: specialties,
            Sections: sections);
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? previousClinicId = _currentClinicContext.ClinicId;
        bool wasAdmin = _currentClinicContext.IsPlatformAdmin;
        try
        {
            _currentClinicContext.SetTenant(clinicId);
            return await action();
        }
        finally
        {
            if (wasAdmin)
            {
                _currentClinicContext.SetPlatformAdmin();
            }
            else if (previousClinicId.HasValue)
            {
                _currentClinicContext.SetTenant(previousClinicId.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }

    private static IReadOnlyList<string> DeserializePhones(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller for owner clinic creation, switching, resumable onboarding, and clinic settings.
/// </summary>
[ApiController]
[Route("api/v1/clinics")]
[Authorize]
public sealed class ClinicsController : ControllerBase
{
    private readonly IClinicManagementService _clinicService;

    public ClinicsController(IClinicManagementService clinicService)
    {
        _clinicService = clinicService ?? throw new ArgumentNullException(nameof(clinicService));
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<MyClinicCardViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.ListMyClinicsAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var cards = result.Value.Select(MapCardViewModel).ToList();
        return Ok(cards);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MyClinicCardViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateClinicDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string deviceId = dto.DeviceId ?? User.FindFirst("deviceId")?.Value ?? "web-browser";
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _clinicService.CreateClinicAsync(
            userId,
            dto.Name,
            dto.TimeZone,
            dto.Currency,
            dto.RoundingRule,
            deviceId,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        if (!string.IsNullOrEmpty(result.Value.AccessToken) && !string.IsNullOrEmpty(result.Value.RefreshToken))
        {
            SetStaffCookies(result.Value.AccessToken, result.Value.RefreshToken);
        }

        return Created($"/api/v1/clinics/{result.Value.ClinicId}/dashboard", MapCardViewModel(result.Value));
    }

    [HttpPost("{clinicId:guid}/switch")]
    [ProducesResponseType(typeof(SwitchClinicViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Switch(Guid clinicId, [FromQuery] string? deviceId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string effectiveDeviceId = deviceId ?? User.FindFirst("deviceId")?.Value ?? "web-browser";
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _clinicService.SwitchClinicAsync(
            userId,
            clinicId,
            effectiveDeviceId,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        SetStaffCookies(result.Value.AccessToken, result.Value.RefreshToken);
        return Ok(new SwitchClinicViewModel(
            result.Value.ClinicId,
            result.Value.Name,
            result.Value.Slug,
            result.Value.Status,
            result.Value.OnboardingStatus,
            result.Value.AccessToken,
            result.Value.RefreshToken,
            result.Value.ExpiresAtUtc));
    }

    [HttpGet("slug-check")]
    [ProducesResponseType(typeof(SlugCheckViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckSlug([FromQuery] string? slug, [FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        var result = await _clinicService.CheckSlugAsync(slug, clinicId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new SlugCheckViewModel(
            result.Value.Slug,
            result.Value.IsValid,
            result.Value.IsAvailable,
            result.Value.ErrorCode,
            result.Value.Reason,
            result.Value.SuggestedAlternative,
            result.Value.PreviewUrl));
    }

    [HttpGet("specialties/catalog")]
    [ProducesResponseType(typeof(IReadOnlyList<SpecialtyCatalogItemViewModel>), StatusCodes.Status200OK)]
    public IActionResult GetSpecialtyCatalog()
    {
        var items = _clinicService.GetSpecialtyCatalog()
            .Select(s => new SpecialtyCatalogItemViewModel(
                s.CatalogCode,
                s.NameAr,
                s.NameEn,
                s.HasDentalPlugin,
                s.DefaultFormSchemaJson,
                s.DefaultAppointmentCategoriesJson))
            .ToList();

        return Ok(items);
    }

    [HttpGet("{clinicId:guid}/onboarding")]
    [ProducesResponseType(typeof(OnboardingStateViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOnboarding(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetOnboardingStateAsync(userId, clinicId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapOnboardingViewModel(result.Value));
    }

    [HttpPost("{clinicId:guid}/onboarding/welcome")]
    [ProducesResponseType(typeof(OnboardingStateViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteWelcome(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.CompleteWelcomeStepAsync(
            userId,
            clinicId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapOnboardingViewModel(result.Value));
    }

    [HttpPut("{clinicId:guid}/onboarding/details")]
    [ProducesResponseType(typeof(OnboardingStateViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveOnboardingDetails(
        Guid clinicId,
        [FromBody] OnboardingDetailsDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.SaveOnboardingDetailsAsync(
            userId,
            clinicId,
            dto.Name,
            dto.Street,
            dto.City,
            dto.Governorate,
            dto.MainPhone,
            dto.ExtraPhones,
            dto.ContactEmail,
            dto.LogoAssetId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapOnboardingViewModel(result.Value));
    }

    [HttpPost("{clinicId:guid}/onboarding/logo")]
    [ProducesResponseType(typeof(LogoUploadViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogo(
        Guid clinicId,
        [FromBody] UploadLogoDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(dto.ContentBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "LOGO_INVALID_BASE64",
                Detail = "Logo payload is not valid base64."
            });
        }

        var result = await _clinicService.UploadClinicLogoAsync(
            userId,
            clinicId,
            dto.FileName,
            dto.ContentType,
            rawBytes,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new LogoUploadViewModel(
            result.Value.AssetId,
            result.Value.ContentType,
            result.Value.SizeBytes,
            result.Value.FullStorageKey,
            result.Value.ThumbStorageKey));
    }

    [HttpPut("{clinicId:guid}/onboarding/types")]
    [ProducesResponseType(typeof(OnboardingStateViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveOnboardingTypes(
        Guid clinicId,
        [FromBody] OnboardingTypesDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var selections = (dto.Specialties ?? Array.Empty<SpecialtySelectionDto>())
            .Select(s => new SpecialtySelectionInput(s.CatalogCode, s.CustomNameAr, s.CustomNameEn))
            .ToList();

        var result = await _clinicService.SaveOnboardingTypesAsync(
            userId,
            clinicId,
            selections,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapOnboardingViewModel(result.Value));
    }

    [HttpPut("{clinicId:guid}/onboarding/slug")]
    [ProducesResponseType(typeof(OnboardingStateViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveOnboardingSlug(
        Guid clinicId,
        [FromBody] OnboardingSlugDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.SaveOnboardingSlugAsync(
            userId,
            clinicId,
            dto.Slug,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapOnboardingViewModel(result.Value));
    }

    [HttpPost("{clinicId:guid}/onboarding/complete")]
    [ProducesResponseType(typeof(ClinicDashboardViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteOnboarding(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.CompleteOnboardingAsync(
            userId,
            clinicId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapDashboardViewModel(result.Value));
    }

    [HttpGet("{clinicId:guid}/dashboard")]
    [ProducesResponseType(typeof(ClinicDashboardViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDashboard(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetClinicDashboardAsync(userId, clinicId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapDashboardViewModel(result.Value));
    }

    [HttpGet("{clinicId:guid}/settings")]
    [ProducesResponseType(typeof(ClinicSettingsViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetClinicSettingsAsync(userId, clinicId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapSettingsViewModel(result.Value));
    }

    [HttpPut("{clinicId:guid}/settings")]
    [ProducesResponseType(typeof(ClinicSettingsViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicSettingsDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicService.UpdateClinicSettingsAsync(
            userId,
            clinicId,
            dto.Name,
            dto.Street,
            dto.City,
            dto.Governorate,
            dto.MainPhone,
            dto.ExtraPhones,
            dto.ContactEmail,
            dto.Slug,
            dto.TimeZone,
            dto.Currency,
            dto.RoundingRule,
            dto.LogoAssetId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapSettingsViewModel(result.Value));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private void SetStaffCookies(string accessToken, string refreshToken)
    {
        bool isSecure = Request.IsHttps;
        Response.Cookies.Append("__Host-Staff-Token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        });
        Response.Cookies.Append("__Host-Staff-Refresh", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private static MyClinicCardViewModel MapCardViewModel(MyClinicCardResult card) => new(
        card.ClinicId,
        card.Name,
        card.Slug,
        card.Status,
        card.OnboardingStatus,
        card.TimeZone,
        card.Currency,
        card.RoundingRule,
        card.LogoAssetId,
        card.ModulesEnabled,
        card.CompletedSteps,
        card.WelcomeSeen,
        card.AccessToken,
        card.RefreshToken,
        card.ExpiresAtUtc);

    private static OnboardingStateViewModel MapOnboardingViewModel(OnboardingStateResult state) => new(
        state.ClinicId,
        state.Name,
        state.Slug,
        state.OnboardingStatus,
        state.WelcomeSeen,
        state.CompletedSteps,
        state.Street,
        state.City,
        state.Governorate,
        state.MainPhone,
        state.ExtraPhones,
        state.ContactEmail,
        state.LogoAssetId,
        state.Specialties.Select(s => new ClinicSpecialtyItemViewModel(
            s.Id,
            s.CatalogCode,
            s.NameAr,
            s.NameEn,
            s.HasDentalPlugin,
            s.DefaultFormSchemaJson,
            s.DefaultAppointmentCategoriesJson)).ToList());

    private static ClinicDashboardViewModel MapDashboardViewModel(ClinicDashboardResult d) => new(
        d.ClinicId,
        d.Name,
        d.Slug,
        d.Status,
        d.OnboardingStatus,
        d.ModulesEnabled,
        d.AwaitingModuleSetup,
        d.StatusMessageAr,
        d.StatusMessageEn,
        d.OwnerChecklist,
        d.SpecialtiesCount);

    private static ClinicSettingsViewModel MapSettingsViewModel(ClinicSettingsResult s) => new(
        s.ClinicId,
        s.Name,
        s.Slug,
        s.Street,
        s.City,
        s.Governorate,
        s.MainPhone,
        s.ExtraPhones,
        s.ContactEmail,
        s.TimeZone,
        s.Currency,
        s.RoundingRule,
        s.HasFinancialEntries,
        s.LogoAssetId,
        s.Specialties.Select(sp => new ClinicSpecialtyItemViewModel(
            sp.Id,
            sp.CatalogCode,
            sp.NameAr,
            sp.NameEn,
            sp.HasDentalPlugin,
            sp.DefaultFormSchemaJson,
            sp.DefaultAppointmentCategoriesJson)).ToList(),
        s.Sections.Select(sec => new SettingsSectionViewModel(
            sec.SectionKey,
            sec.TitleAr,
            sec.TitleEn,
            sec.RequiredPermission,
            sec.ModuleKey)).ToList());
}


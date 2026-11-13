using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.Filters;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Isolated Patient Portal controller under /api/v1/c/{slug}/portal.
/// Never accepts a patient ID from client input; patient identity comes strictly from the PatientPortal token/cookie.
/// </summary>
[ApiController]
[Route("api/v1/c/{slug}/portal")]
[PortalSecurityHeaders]
public sealed class PatientPortalController : ControllerBase
{
    public const string PortalCookieName = "__Host-Portal-Token";
    private readonly IPatientPortalService _portalService;

    public PatientPortalController(IPatientPortalService portalService)
    {
        _portalService = portalService ?? throw new ArgumentNullException(nameof(portalService));
    }

    [HttpGet("landing")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicClinicLandingViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLanding(string slug, CancellationToken cancellationToken)
    {
        var result = await _portalService.ResolvePublicLandingAsync(slug, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        if (result.Value.IsRedirect)
        {
            Response.Headers["X-Canonical-Slug"] = result.Value.CurrentSlug;
            Response.Headers["X-Redirect-Target"] = result.Value.RedirectUrl;
        }

        return Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PatientPortalSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        string slug,
        [FromBody] ActivatePatientPortalDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _portalService.ActivateWithEmailCodeAsync(
            slug,
            new ActivatePatientPortalInput(dto.Email, dto.VerificationCode, dto.Password),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        SetPortalCookie(result.Value.AccessToken, result.Value.ExpiresAtUtc);
        return Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("register-pin")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterPrintedPin(
        string slug,
        [FromBody] RegisterPrintedPinDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _portalService.RegisterPrintedPinAndSendEmailCodeAsync(
            slug,
            new RegisterPrintedPinInput(dto.FileNumber, dto.DateOfBirth, dto.PrintedPin, dto.Email),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new { message = "Verification code sent to email." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PatientPortalSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        string slug,
        [FromBody] PatientPortalLoginDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _portalService.LoginAsync(
            slug,
            new PatientPortalLoginInput(dto.Email, dto.Password),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        SetPortalCookie(result.Value.AccessToken, result.Value.ExpiresAtUtc);
        return Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        string slug,
        [FromBody] PatientPortalForgotPasswordDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _portalService.ForgotPasswordAsync(slug, dto.Email, GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new { message = "If that account exists, a reset code was sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(
        string slug,
        [FromBody] PatientPortalResetPasswordDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _portalService.ResetPasswordAsync(
            slug,
            new PatientPortalResetPasswordInput(dto.Email, dto.VerificationCode, dto.NewPassword),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new { message = "Password has been reset." });
    }

    [HttpPost("logout")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(PortalCookieName);
        return Ok(new { message = "Signed out of patient portal." });
    }

    [HttpGet("overview")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(PortalHomeOverviewViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(string slug, CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetHomeOverviewAsync(accountId, clinicId, patientId, slug, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("medical-record")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(PortalMedicalBackgroundViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicalRecord(string slug, CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetMedicalRecordAsync(accountId, clinicId, patientId, slug, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("visits")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalVisitSummaryViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisits(string slug, CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetVisitsSectionAsync(accountId, clinicId, patientId, slug, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(ClinicalViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("visits/{visitId:guid}")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(PortalVisitDetailViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisitDetail(string slug, Guid visitId, CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetVisitDetailAsync(accountId, clinicId, patientId, slug, visitId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("treatment-plans")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalTreatmentPlanItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreatmentPlans(string slug, CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetTreatmentPlansSectionAsync(accountId, clinicId, patientId, slug, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(PatientPortalViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("prescriptions/{prescriptionId:guid}/signed-url")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(PortalSignedUrlViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePrescriptionSignedUrl(
        string slug,
        Guid prescriptionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.CreateSignedPrescriptionUrlAsync(accountId, clinicId, patientId, slug, prescriptionId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("attachments/{attachmentId:guid}/signed-url")]
    [Authorize(Policy = "portal.patient")]
    [ProducesResponseType(typeof(PortalSignedUrlViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAttachmentSignedUrl(
        string slug,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.CreateSignedAttachmentUrlAsync(accountId, clinicId, patientId, slug, attachmentId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("downloads/{kind}/{resourceId:guid}")]
    [Authorize(Policy = "portal.patient")]
    public async Task<IActionResult> DownloadSigned(
        string slug,
        string kind,
        Guid resourceId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (!TryGetPortalIdentity(out _, out _, out Guid patientId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ExecuteSignedDownloadAsync(slug, kind, resourceId, patientId, token, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private void SetPortalCookie(string accessToken, DateTimeOffset expiresAtUtc)
    {
        Response.Cookies.Append(PortalCookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc
        });
    }

    private bool TryGetPortalIdentity(out Guid accountId, out Guid clinicId, out Guid patientId)
    {
        accountId = Guid.Empty;
        clinicId = Guid.Empty;
        patientId = Guid.Empty;

        string? realm = User.FindFirstValue("realm");
        if (!string.Equals(realm, "PatientPortal", StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId)
               && Guid.TryParse(User.FindFirstValue("clinic_id"), out clinicId)
               && Guid.TryParse(User.FindFirstValue("patient_id"), out patientId);
    }

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetRequestId() => HttpContext.TraceIdentifier;
}


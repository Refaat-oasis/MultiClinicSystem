using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Staff-facing endpoints for inviting patients to the portal, controlling visibility flags,
/// updating clinic portal settings, and reading patient portal access logs.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/portal")]
[Authorize]
public sealed class StaffPortalManagementController : ControllerBase
{
    private readonly IPatientPortalService _portalService;
    private readonly IPatientService _patientService;
    private readonly IClinicalService _clinicalService;

    public StaffPortalManagementController(
        IPatientPortalService portalService,
        IPatientService patientService,
        IClinicalService clinicalService)
    {
        _portalService = portalService ?? throw new ArgumentNullException(nameof(portalService));
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
        _clinicalService = clinicalService ?? throw new ArgumentNullException(nameof(clinicalService));
    }

    [HttpGet("patients/{patientId:guid}")]
    [ProducesResponseType(typeof(PatientPortalAdminStatusViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientStatus(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetPatientPortalAdminStatusAsync(staffUserId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("patients/{patientId:guid}/invite")]
    [ProducesResponseType(typeof(PatientPortalAdminStatusViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> InvitePatient(
        Guid clinicId,
        Guid patientId,
        [FromBody] InvitePatientToPortalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.InvitePatientAsync(
            staffUserId,
            clinicId,
            patientId,
            new InvitePatientToPortalInput(
                dto.Email,
                dto.IsGuardianAccount,
                dto.GuardianName,
                dto.ConsentAccepted,
                dto.TermsVersion,
                dto.IssuePrintedPinIfNoEmail),
            GetDeviceId(),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("patients/{patientId:guid}/resend")]
    [ProducesResponseType(typeof(PatientPortalAdminStatusViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendInvitation(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ResendInvitationAsync(
            staffUserId, clinicId, patientId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("patients/{patientId:guid}/status")]
    [ProducesResponseType(typeof(PatientPortalAdminStatusViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAccountEnabled(
        Guid clinicId,
        Guid patientId,
        [FromBody] SetPortalAccountEnabledDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.SetAccountEnabledAsync(
            staffUserId, clinicId, patientId, dto.Enabled, dto.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("patients/{patientId:guid}/profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePatientPortalProfile(
        Guid clinicId,
        Guid patientId,
        [FromBody] UpdatePatientPortalProfileDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _patientService.UpdatePatientPortalProfileAsync(
            staffUserId,
            clinicId,
            patientId,
            dto.Email,
            dto.BloodType,
            dto.GuardianName,
            dto.GuardianEmail,
            GetDeviceId(),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : NoContent();
    }

    [HttpPut("patients/{patientId:guid}/notes/{noteId:guid}/visibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetNoteVisibility(
        Guid clinicId,
        Guid patientId,
        Guid noteId,
        [FromBody] SetPortalVisibilityDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _patientService.SetNotePortalVisibilityAsync(
            staffUserId, clinicId, patientId, noteId, dto.IsVisible, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : NoContent();
    }

    [HttpPut("patients/{patientId:guid}/attachments/{attachmentId:guid}/visibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetAttachmentVisibility(
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        [FromBody] SetPortalVisibilityDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _patientService.SetAttachmentPortalVisibilityAsync(
            staffUserId, clinicId, patientId, attachmentId, dto.IsVisible, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : NoContent();
    }

    [HttpPut("visits/{visitId:guid}/visibility")]
    [ProducesResponseType(typeof(VisitSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetVisitVisibility(
        Guid clinicId,
        Guid visitId,
        [FromBody] SetPortalVisibilityDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.SetVisitPortalVisibilityAsync(
            staffUserId, clinicId, visitId, dto.IsVisible, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ClinicPortalSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetClinicPortalSettingsAsync(staffUserId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ClinicPortalSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicPortalSettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.UpdateClinicPortalSettingsAsync(
            staffUserId,
            clinicId,
            new UpdateClinicPortalSettingInput(
                dto.ShowPricesByDefault,
                dto.ShowDoctorNames,
                dto.DefaultVisitVisibility,
                dto.TermsTextAr,
                dto.TermsTextEn,
                dto.WelcomeMessageAr,
                dto.WelcomeMessageEn),
            GetDeviceId(),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("access-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalAccessLogItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAccessLogs(
        Guid clinicId,
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ListPatientPortalAccessLogsAsync(
            staffUserId, clinicId, patientId, page, pageSize, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(PatientPortalViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("booking-settings")]
    [ProducesResponseType(typeof(ClinicPortalBookingSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookingSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.GetClinicPortalBookingSettingsAsync(
            staffUserId, clinicId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("booking-settings")]
    [ProducesResponseType(typeof(ClinicPortalBookingSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBookingSettings(
        Guid clinicId,
        [FromBody] UpdateClinicPortalBookingSettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.UpdateClinicPortalBookingSettingsAsync(
            staffUserId,
            clinicId,
            new UpdateClinicPortalBookingSettingInput(
                dto.Mode,
                dto.MinLeadTimeMinutes,
                dto.MaxDaysAhead,
                dto.SlotStepMinutes,
                dto.MaxActiveFutureBookingsPerPatient,
                dto.MaxBookingsPerDay,
                dto.CancellationCutoffHours,
                dto.AutoBlockAfterNoShows,
                dto.ApprovalHoldHours,
                dto.ShowPricesWhenAvailable),
            GetDeviceId(),
            GetIp(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("booking-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalBookingRequestItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBookingRequests(
        Guid clinicId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ListPortalBookingRequestsAsync(
            staffUserId, clinicId, status, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(PatientPortalViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("booking-requests/{appointmentId:guid}/approve")]
    [ProducesResponseType(typeof(PortalBookingRequestItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveBookingRequest(
        Guid clinicId,
        Guid appointmentId,
        [FromBody] ReviewPortalBookingRequestDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ApprovePortalBookingRequestAsync(
            staffUserId, clinicId, appointmentId, dto?.ReviewNotes, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("booking-requests/{appointmentId:guid}/decline")]
    [ProducesResponseType(typeof(PortalBookingRequestItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeclineBookingRequest(
        Guid clinicId,
        Guid appointmentId,
        [FromBody] ReviewPortalBookingRequestDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.DeclinePortalBookingRequestAsync(
            staffUserId, clinicId, appointmentId, dto?.ReviewNotes, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("booking-requests/expire-stale")]
    [ProducesResponseType(typeof(PortalExpiredRequestsViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExpireStaleBookingRequests(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.ExpireStalePortalBookingRequestsAsync(
            staffUserId, clinicId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new PortalExpiredRequestsViewModel(result.Value));
    }

    [HttpPut("patients/{patientId:guid}/booking-block")]
    [ProducesResponseType(typeof(PatientPortalAdminStatusViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPatientBookingBlock(
        Guid clinicId,
        Guid patientId,
        [FromBody] SetPatientOnlineBookingBlockDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetStaffUserId(out Guid staffUserId))
        {
            return Unauthorized();
        }

        var result = await _portalService.SetPatientOnlineBookingBlockAsync(
            staffUserId, clinicId, patientId, dto.Blocked, dto.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientPortalViewModelMapper.ToViewModel(result.Value));
    }

    private bool TryGetStaffUserId(out Guid userId)
    {
        userId = Guid.Empty;
        string? realm = User.FindFirstValue("realm");
        if (string.Equals(realm, "PatientPortal", StringComparison.Ordinal) ||
            string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private string? GetDeviceId()
        => User.FindFirstValue("deviceId") ?? Request.Headers["X-Device-Id"].FirstOrDefault();

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetRequestId() => HttpContext.TraceIdentifier;
}


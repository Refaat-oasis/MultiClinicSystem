using System.Security.Claims;
using BuildingBlocks.Domain;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Transport layer for Super Admin clinic directory, lifecycle updates, ownership transfer, and read-only inspect mode.
/// </summary>
[ApiController]
[Route("api/v1/platform/clinics")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformClinicsController : ControllerBase
{
    private readonly IPlatformAdminService _platformService;
    private readonly IPatientService _patientService;

    public PlatformClinicsController(IPlatformAdminService platformService, IPatientService patientService)
    {
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OwnerSummaryViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListClinics([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await _platformService.GetClinicDirectoryAsync(search, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(o => new OwnerSummaryViewModel(
            o.OwnerId,
            o.UserId,
            o.FullName,
            o.Email,
            o.PhoneNumber,
            o.Status,
            o.EmailConfirmed,
            o.MaxClinics,
            o.ClinicsCount,
            o.CreatedAtUtc,
            o.Clinics.Select(c => new ClinicSummaryViewModel(
                c.ClinicId,
                c.Name,
                c.Slug,
                c.Status,
                c.SubscriptionExpiryUtc,
                c.GraceDays,
                c.IsActive,
                c.CreatedAtUtc,
                c.ModulesEnabled)).ToList())).ToList());
    }

    [HttpPut("{clinicId:guid}/status")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid clinicId,
        [FromBody] UpdateClinicStatusDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.UpdateClinicStatusAsync(
            adminId,
            clinicId,
            dto.Status,
            dto.SubscriptionExpiryUtc,
            dto.GraceDays,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم تحديث حالة العيادة بنجاح. / Clinic status updated."));
    }

    [HttpPost("{clinicId:guid}/move")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveClinic(
        Guid clinicId,
        [FromBody] MoveClinicDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.MoveClinicOwnerAsync(
            adminId,
            clinicId,
            dto.TargetOwnerId,
            dto.StepUpPassword,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم نقل ملكية العيادة بنجاح. / Clinic ownership transferred."));
    }

    [HttpGet("{clinicId:guid}/inspect")]
    [ProducesResponseType(typeof(InspectClinicViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InspectClinic(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.InspectClinicAsync(
            adminId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var v = result.Value;
        return Ok(new InspectClinicViewModel(
            v.ClinicId,
            v.Name,
            v.Slug,
            v.Status,
            v.OwnerId,
            v.SubscriptionExpiryUtc,
            v.GraceDays,
            v.SampleRecordsCount,
            v.SampleRecordsTotalAmount.Amount,
            v.SampleRecordsTotalAmount.Currency,
            v.ReadOnlyMode));
    }

    [HttpGet("{clinicId:guid}/modules")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicModuleStateViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClinicModules(Guid clinicId, CancellationToken cancellationToken)
    {
        var result = await _platformService.GetClinicModulesAsync(clinicId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(MapModuleViewModel).ToList());
    }

    [HttpPut("{clinicId:guid}/modules")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicModuleStateViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateClinicModules(
        Guid clinicId,
        [FromBody] UpdateClinicModulesDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();
        var inputs = (dto.Modules ?? new List<ModuleToggleItemDto>())
            .Select(m => new ModuleToggleInput(m.ModuleKey, m.Enabled, m.Note))
            .ToList();

        var result = await _platformService.UpdateClinicModulesAsync(
            adminId,
            clinicId,
            inputs,
            dto.StepUpPassword,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(MapModuleViewModel).ToList());
    }

    [HttpGet("{clinicId:guid}/inspect/patients/{patientId:guid}")]
    [ProducesResponseType(typeof(PatientDetailViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectPatient(Guid clinicId, Guid patientId, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        var result = await _patientService.InspectPatientAsSuperAdminAsync(
            adminId.ToString(), clinicId, patientId, reason, cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientViewModelMapper.MapDetail(result.Value));
    }

    [HttpGet("{clinicId:guid}/inspect/schedule")]
    [ProducesResponseType(typeof(ClinicScheduleInspectViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectSchedule(
        Guid clinicId,
        [FromQuery] string? date,
        [FromServices] ISchedulingService schedulingService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        var result = await schedulingService.InspectScheduleAsSuperAdminAsync(adminId, clinicId, date, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("{clinicId:guid}/inspect/clinical")]
    [ProducesResponseType(typeof(ClinicClinicalInspectViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectClinical(
        Guid clinicId,
        [FromServices] IClinicalService clinicalService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        var result = await clinicalService.InspectClinicClinicalAsync(adminId, clinicId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("{clinicId:guid}/inspect/billing")]
    [ProducesResponseType(typeof(ClinicBillingInspectViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectBilling(
        Guid clinicId,
        [FromServices] IBillingService billingService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        var result = await billingService.InspectClinicBillingAsync(adminId, clinicId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{clinicId:guid}/inspect/{*subpath}")]
    [HttpPut("{clinicId:guid}/inspect/{*subpath}")]
    [HttpDelete("{clinicId:guid}/inspect/{*subpath}")]
    [HttpPatch("{clinicId:guid}/inspect/{*subpath}")]
    [HttpPost("{clinicId:guid}/inspect")]
    [HttpPut("{clinicId:guid}/inspect")]
    [HttpDelete("{clinicId:guid}/inspect")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult RejectInspectWrite(Guid clinicId, string? subpath = null)
    {
        return ProblemDetailsMapper.ToProblemDetails(
            Error.Forbidden("INSPECT_MODE_READ_ONLY", "Inspect mode is strictly read-only. Writes are forbidden."));
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out adminId);
    }

    private static ClinicModuleStateViewModel MapModuleViewModel(ClinicModuleStateResult m) => new(
        m.ModuleKey,
        m.TitleAr,
        m.TitleEn,
        m.DescriptionAr,
        m.DescriptionEn,
        m.Enabled,
        m.Prerequisites,
        m.MissingPrerequisites,
        m.EnabledDependents,
        m.ChangedAtUtc,
        m.Note);
}


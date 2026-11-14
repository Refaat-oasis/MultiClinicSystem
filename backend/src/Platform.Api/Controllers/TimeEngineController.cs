using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for the Phase 13 adaptive average-time engine,
/// clinic thresholds, per-doctor toggles, excused sessions, and background recalculation.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/time-engine")]
[Authorize]
public sealed class TimeEngineController : ControllerBase
{
    private readonly ITimeEngineService _timeEngineService;

    public TimeEngineController(ITimeEngineService timeEngineService)
    {
        _timeEngineService = timeEngineService ?? throw new ArgumentNullException(nameof(timeEngineService));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ClinicTimeEngineSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _timeEngineService.GetClinicSettingsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TimeEngineViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ClinicTimeEngineSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicTimeEngineSettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpdateClinicTimeEngineSettingInput(
            dto.MinSampleSize,
            dto.TrimFraction,
            dto.MaxStepChangePercent,
            dto.OperationId);

        var result = await _timeEngineService.UpdateClinicSettingsAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TimeEngineViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorTimeEngineSettingViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDoctorSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _timeEngineService.ListDoctorSettingsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(TimeEngineViewModelMapper.ToViewModel).ToList());
    }

    [HttpPut("doctors/{doctorId:guid}/enabled")]
    [ProducesResponseType(typeof(DoctorTimeEngineSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetDoctorEnabled(
        Guid clinicId,
        Guid doctorId,
        [FromBody] SetDoctorTimeEngineDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new SetDoctorTimeEngineInput(dto.IsEngineEnabled, dto.OperationId);
        var result = await _timeEngineService.SetDoctorEngineEnabledAsync(
            userId, clinicId, doctorId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TimeEngineViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTimeStatViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStats(
        Guid clinicId,
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _timeEngineService.ListCategoryStatsAsync(
            userId, clinicId, doctorId, categoryId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(TimeEngineViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("durations")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDurationViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDurations(
        Guid clinicId,
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _timeEngineService.ListSessionDurationsAsync(
            userId, clinicId, doctorId, categoryId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(TimeEngineViewModelMapper.ToViewModel).ToList());
    }

    [HttpPut("visits/{visitId:guid}/excused")]
    [ProducesResponseType(typeof(SessionDurationViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetVisitExcused(
        Guid clinicId,
        Guid visitId,
        [FromBody] SetSessionDurationExcusedDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new SetSessionDurationExcusedInput(dto.IsExcused, dto.ExcuseReason, dto.OperationId);
        var result = await _timeEngineService.SetSessionDurationExcusedAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TimeEngineViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTimeStatViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recalculate(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _timeEngineService.RecalculateClinicStatsAsync(
            userId, clinicId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(TimeEngineViewModelMapper.ToViewModel).ToList());
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    private string? GetDeviceId() => User.FindFirstValue("device_id");
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.TraceIdentifier;
}


using System.Security.Claims;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 15 offline sync:
/// push batches, incremental cursor pull, number block allocation, conflict inbox, device status, and settings.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/sync")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ClinicSyncSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.GetSettingsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ClinicSyncSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicSyncSettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.UpdateSettingsAsync(
            userId,
            clinicId,
            new UpdateClinicSyncSettingInput(
                dto.OfflineSessionMaxHours,
                dto.ReceiptBlockSize,
                dto.ReceiptBlockLowWaterMark,
                dto.ClockSkewWarningSeconds),
            GetDeviceId(),
            GetIpAddress(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("number-blocks")]
    [ProducesResponseType(typeof(AllocatedNumberBlockViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AllocateNumberBlock(
        Guid clinicId,
        [FromBody] AllocateNumberBlockDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.AllocateNumberBlockAsync(
            userId,
            clinicId,
            new AllocateNumberBlockInput(dto.DeviceId, dto.Kind, dto.RequestedSize),
            GetIpAddress(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("number-blocks")]
    [ProducesResponseType(typeof(IReadOnlyList<AllocatedNumberBlockViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNumberBlocks(
        Guid clinicId,
        [FromQuery] string? deviceId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.ListNumberBlocksAsync(userId, clinicId, deviceId ?? GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SyncViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("push")]
    [ProducesResponseType(typeof(SyncPushBatchViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Push(
        Guid clinicId,
        [FromBody] SyncPushBatchDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string effectiveDeviceId = string.IsNullOrWhiteSpace(dto.DeviceId)
            ? (GetDeviceId() ?? string.Empty)
            : dto.DeviceId;

        var input = new SyncPushBatchInput(
            effectiveDeviceId,
            (dto.Operations ?? Array.Empty<SyncPushOperationDto>())
                .Select(o => new SyncPushOperationInput(
                    o.OperationId,
                    o.ActionType,
                    o.EntityType,
                    o.EntityId,
                    o.PayloadJson,
                    o.ClientTimestampUtc,
                    o.ClientBaseCursor))
                .ToList());

        var result = await _syncService.PushAsync(
            userId,
            clinicId,
            input,
            GetIpAddress(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("pull")]
    [ProducesResponseType(typeof(SyncPullResponseViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pull(
        Guid clinicId,
        [FromQuery] long cursor = 0,
        [FromQuery] int limit = 100,
        [FromQuery] string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.PullAsync(
            userId,
            clinicId,
            deviceId ?? GetDeviceId(),
            cursor,
            limit,
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("conflicts")]
    [ProducesResponseType(typeof(IReadOnlyList<SyncConflictViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListConflicts(
        Guid clinicId,
        [FromQuery] SyncConflictStatus? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.ListConflictsAsync(userId, clinicId, status, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SyncViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("conflicts/{conflictId:guid}/resolve")]
    [ProducesResponseType(typeof(SyncConflictViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveConflict(
        Guid clinicId,
        Guid conflictId,
        [FromBody] ResolveSyncConflictDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.ResolveConflictAsync(
            userId,
            clinicId,
            conflictId,
            new ResolveSyncConflictInput(dto.Resolution, dto.ResolutionNote),
            GetDeviceId(),
            GetIpAddress(),
            GetRequestId(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SyncViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(IReadOnlyList<SyncDeviceStateViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDevices(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _syncService.ListDevicesAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SyncViewModelMapper.ToViewModel).ToList());
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() =>
        User.FindFirst("device_id")?.Value
        ?? Request.Headers["X-Device-Id"].FirstOrDefault();

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetRequestId() => HttpContext.TraceIdentifier;
}


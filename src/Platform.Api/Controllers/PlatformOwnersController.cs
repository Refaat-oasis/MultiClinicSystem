using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Transport layer for Super Admin management of clinic owners and clinic quotas.
/// </summary>
[ApiController]
[Route("api/v1/platform/owners")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformOwnersController : ControllerBase
{
    private readonly IPlatformAdminService _platformService;

    public PlatformOwnersController(IPlatformAdminService platformService)
    {
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OwnerSummaryViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOwner([FromBody] CreateOwnerDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.CreateOwnerAsync(
            adminId,
            dto.FullName,
            dto.Email,
            dto.Phone,
            dto.MaxClinics,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var viewModel = MapOwnerSummary(result.Value);
        return CreatedAtAction(nameof(GetOwner), new { ownerId = viewModel.OwnerId }, viewModel);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OwnerSummaryViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOwners([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await _platformService.ListOwnersAsync(search, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(MapOwnerSummary).ToList());
    }

    [HttpGet("{ownerId:guid}")]
    [ProducesResponseType(typeof(OwnerDetailViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwner(Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _platformService.GetOwnerDetailAsync(ownerId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var d = result.Value;
        return Ok(new OwnerDetailViewModel(
            d.OwnerId,
            d.UserId,
            d.FullName,
            d.Email,
            d.PhoneNumber,
            d.Status,
            d.EmailConfirmed,
            d.MaxClinics,
            d.CreatedAtUtc,
            d.Clinics.Select(MapClinicSummary).ToList()));
    }

    [HttpPost("{ownerId:guid}/resend-code")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendInvitationCode(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.ResendOwnerInvitationCodeAsync(
            adminId,
            ownerId,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تمت إعادة إرسال رمز التفعيل. / Activation code resent."));
    }

    [HttpPost("{ownerId:guid}/deactivate")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateOwner(
        Guid ownerId,
        [FromBody] DeactivateOwnerDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.DeactivateOwnerAsync(
            adminId,
            ownerId,
            dto?.StepUpPassword,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم تعطيل حساب المالك بنجاح. / Owner deactivated successfully."));
    }

    [HttpPost("{ownerId:guid}/reactivate")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateOwner(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.ReactivateOwnerAsync(
            adminId,
            ownerId,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تمت إعادة تفعيل حساب المالك. / Owner reactivated."));
    }

    [HttpPut("{ownerId:guid}/quota")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuota(
        Guid ownerId,
        [FromBody] UpdateOwnerQuotaDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _platformService.UpdateOwnerQuotaAsync(
            adminId,
            ownerId,
            dto.MaxClinics,
            dto.StepUpPassword,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم تحديث حصة العيادات بنجاح. / Owner clinic quota updated."));
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out adminId);
    }

    private static OwnerSummaryViewModel MapOwnerSummary(OwnerSummaryResult o) =>
        new(
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
            o.Clinics.Select(MapClinicSummary).ToList());

    private static ClinicSummaryViewModel MapClinicSummary(ClinicSummaryResult c) =>
        new(
            c.ClinicId,
            c.Name,
            c.Slug,
            c.Status,
            c.SubscriptionExpiryUtc,
            c.GraceDays,
            c.IsActive,
            c.CreatedAtUtc,
            c.ModulesEnabled);
}


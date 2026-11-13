using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller allowing clinic owners to export a full tenant-isolated ZIP archive on demand.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/export")]
[Authorize]
public sealed class ClinicExportController : ControllerBase
{
    private readonly IClinicExportService _clinicExportService;

    public ClinicExportController(IClinicExportService clinicExportService)
    {
        _clinicExportService = clinicExportService ?? throw new ArgumentNullException(nameof(clinicExportService));
    }

    [HttpGet]
    public async Task<IActionResult> ExportClinicData(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        if (Request.Headers.TryGetValue("X-Offline-Sync", out var offlineHeader) &&
            string.Equals(offlineHeader.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                title = "Online Connection Required",
                status = StatusCodes.Status400BadRequest,
                code = "ONLINE_ONLY_ACTION_FORBIDDEN",
                detail = "Clinic full data export requires a direct online connection.",
            });
        }

        var result = await _clinicExportService.ExportClinicDataAsync(
            userId,
            clinicId,
            GetDeviceId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() =>
        User.FindFirst("device_id")?.Value
        ?? Request.Headers["X-Device-Id"].FirstOrDefault();
}


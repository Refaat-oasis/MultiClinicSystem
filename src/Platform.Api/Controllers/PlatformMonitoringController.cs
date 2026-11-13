using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Transport layer for Super Admin dashboard metrics and platform audit log inspection.
/// </summary>
[ApiController]
[Route("api/v1/platform")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformMonitoringController : ControllerBase
{
    private readonly IPlatformAdminService _platformService;

    public PlatformMonitoringController(IPlatformAdminService platformService)
    {
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PlatformDashboardViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _platformService.GetDashboardAsync(cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var d = result.Value;
        return Ok(new PlatformDashboardViewModel(
            d.TotalOwners,
            d.ActiveOwners,
            d.TotalClinics,
            d.ActiveClinics,
            d.PendingOnboardingClinics,
            d.ReadOnlyClinics,
            d.SuspendedClinics,
            d.ClinicsAwaitingModuleSetup,
            d.ExpiringSubscriptionsCount,
            d.FailedLoginsLast24Hours,
            d.PendingOutboxMessages,
            d.RecentAuditEntries.Select(MapAudit).ToList()));
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? clinicId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _platformService.GetAuditLogsAsync(clinicId, action, page, pageSize, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(MapAudit).ToList());
    }

    private static AuditLogItemViewModel MapAudit(AuditLogItemResult a) =>
        new(
            a.Id,
            a.UserId,
            a.ClinicId,
            a.Action,
            a.EntityName,
            a.EntityId,
            a.OldValues,
            a.NewValues,
            a.TimestampUtc,
            a.IpAddress,
            a.RequestId);
}


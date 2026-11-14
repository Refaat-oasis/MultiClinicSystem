using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Admin-host only Super Admin cross-clinic aggregate reports controller.
/// </summary>
[ApiController]
[Route("api/v1/platform/reports")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public PlatformReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService ?? throw new ArgumentNullException(nameof(reportsService));
    }

    [HttpGet("cross-clinic")]
    [ProducesResponseType(typeof(PlatformCrossClinicReportViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CrossClinic(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out Guid adminId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.GetPlatformCrossClinicReportAsync(adminId, fromDate, toDate, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }
}


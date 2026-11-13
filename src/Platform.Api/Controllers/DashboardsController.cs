using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller for modular dashboard widgets, per-user layouts, and multi-clinic Owner Portfolio.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class DashboardsController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IOwnerPortfolio _ownerPortfolio;

    public DashboardsController(IDashboardService dashboardService, IOwnerPortfolio ownerPortfolio)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _ownerPortfolio = ownerPortfolio ?? throw new ArgumentNullException(nameof(ownerPortfolio));
    }

    [HttpGet("clinics/{clinicId:guid}/dashboards/widgets")]
    [ProducesResponseType(typeof(DashboardWidgetCatalogViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWidgets(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string? deviceId = User.FindFirst("deviceId")?.Value;
        var result = await _dashboardService.GetAvailableWidgetsAsync(userId, clinicId, deviceId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var v = result.Value;
        return Ok(new DashboardWidgetCatalogViewModel(
            v.ClinicId,
            v.Widgets.Select(w => new DashboardWidgetDefinitionViewModel(
                w.WidgetKey, w.ModuleKey, w.RequiredPermission, w.AllowedScope, w.TitleAr, w.TitleEn)).ToList(),
            v.PinnedWidgetKeys));
    }

    [HttpGet("clinics/{clinicId:guid}/dashboards/widgets/{widgetKey}")]
    [ProducesResponseType(typeof(DashboardWidgetDataViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryWidget(
        Guid clinicId,
        string widgetKey,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string? deviceId = User.FindFirst("deviceId")?.Value;
        var result = await _dashboardService.QueryWidgetAsync(userId, clinicId, widgetKey, from, to, scope, deviceId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var v = result.Value;
        return Ok(new DashboardWidgetDataViewModel(
            v.WidgetKey, v.ModuleKey, v.Scope, v.FromUtc, v.ToUtc,
            v.TitleAr, v.TitleEn, v.PrimaryValue, v.UnitLabel, v.Breakdown));
    }

    [HttpPut("clinics/{clinicId:guid}/dashboards/layout")]
    [ProducesResponseType(typeof(UserDashboardLayoutViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveLayout(
        Guid clinicId,
        [FromBody] SaveDashboardLayoutDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string? deviceId = User.FindFirst("deviceId")?.Value;
        var result = await _dashboardService.SaveLayoutAsync(userId, clinicId, dto.PinnedWidgetKeys, deviceId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new UserDashboardLayoutViewModel(clinicId, result.Value));
    }

    [HttpGet("portfolio")]
    [ProducesResponseType(typeof(OwnerPortfolioViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPortfolio(
        [FromQuery] Guid[]? clinicIds,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _ownerPortfolio.GetPortfolioAsync(userId, clinicIds, from, to, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var p = result.Value;
        return Ok(new OwnerPortfolioViewModel(
            p.OwnerId,
            p.TotalClinics,
            p.ActiveClinics,
            p.TotalStaffAcrossClinics,
            p.TotalDoctorsAcrossClinics,
            p.Clinics.Select(c => new OwnerPortfolioClinicItemViewModel(
                c.ClinicId, c.Name, c.Slug, c.Status, c.OnboardingStatus,
                c.StaffCount, c.RoomsCount, c.DoctorsCount, c.EnabledModulesCount)).ToList()));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}

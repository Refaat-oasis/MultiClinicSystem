using System.Security.Claims;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 14 clinic reports, Dapper queries, and Excel/PDF exports.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService ?? throw new ArgumentNullException(nameof(reportsService));
    }

    [HttpGet("income-vs-expenses")]
    [ProducesResponseType(typeof(IncomeVsExpensesReportViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeVsExpenses(
        Guid clinicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.GetIncomeVsExpensesReportAsync(userId, clinicId, fromDate, toDate, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorFinancialReportRowViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorSummary(
        Guid clinicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? doctorId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.GetDoctorSummaryReportAsync(userId, clinicId, fromDate, toDate, doctorId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("sessions-by-category")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionCategoryReportRowViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionsAndCategories(
        Guid clinicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? doctorId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.GetSessionsAndCategoriesReportAsync(userId, clinicId, fromDate, toDate, doctorId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(IReadOnlyList<MethodTotalReportRowViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentMethodTotals(
        Guid clinicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.GetPaymentMethodTotalsReportAsync(userId, clinicId, fromDate, toDate, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("exports")]
    public async Task<IActionResult> ExportReport(
        Guid clinicId,
        [FromQuery] ReportExportKind reportKind,
        [FromQuery] ReportExportFormat format,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? doctorId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.ExportReportAsync(
            userId, clinicId, reportKind, format, fromDate, toDate, doctorId,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("exports/logs")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportExportLogViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExportLogs(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.ListExportLogsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("scheduled")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledReportConfigViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListScheduledReports(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _reportsService.ListScheduledReportsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("scheduled")]
    [ProducesResponseType(typeof(ScheduledReportConfigViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertScheduledReport(
        Guid clinicId,
        [FromBody] UpsertScheduledReportDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertScheduledReportInput(dto.ReportKind, dto.Format, dto.Frequency, dto.IsEnabled, dto.OperationId);
        var result = await _reportsService.UpsertScheduledReportAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
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


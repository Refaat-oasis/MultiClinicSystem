using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 14 extended clinic finance:
/// fixed costs with recurrence, employee roster, payroll adjustments, payroll runs, and income vs expenses.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/finance")]
[Authorize]
public sealed class FinanceController : ControllerBase
{
    private readonly IFinanceExtendedService _financeService;

    public FinanceController(IFinanceExtendedService financeService)
    {
        _financeService = financeService ?? throw new ArgumentNullException(nameof(financeService));
    }

    [HttpGet("fixed-costs")]
    [ProducesResponseType(typeof(IReadOnlyList<FixedCostViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFixedCosts(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.ListFixedCostsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("fixed-costs")]
    [ProducesResponseType(typeof(FixedCostViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertFixedCost(Guid clinicId, [FromBody] UpsertFixedCostDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertFixedCostInput(
            dto.FixedCostId, dto.Category, dto.TitleAr, dto.TitleEn, dto.Amount, dto.Recurrence,
            dto.PaidFromMethod, dto.EffectiveFromDate, dto.EffectiveToDate, dto.IsActive, dto.Notes, dto.OperationId);
        var result = await _financeService.UpsertFixedCostAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("fixed-costs/{fixedCostId:guid}/void")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidFixedCost(Guid clinicId, Guid fixedCostId, [FromBody] VoidFixedCostDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.VoidFixedCostAsync(
            userId, clinicId, fixedCostId, dto.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value);
    }

    [HttpGet("fixed-costs/occurrences")]
    [ProducesResponseType(typeof(IReadOnlyList<FixedCostOccurrenceViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFixedCostOccurrences(
        Guid clinicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.ListFixedCostOccurrencesAsync(userId, clinicId, fromDate, toDate, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("employees")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEmployees(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.ListEmployeesAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("employees")]
    [ProducesResponseType(typeof(EmployeeViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertEmployee(Guid clinicId, [FromBody] UpsertEmployeeDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertEmployeeInput(
            dto.EmployeeId, dto.FullName, dto.RoleTitle, dto.DoctorId, dto.Phone,
            dto.BaseSalaryAmount, dto.PaidFromMethod, dto.HiredOnDate, dto.IsActive, dto.OperationId);
        var result = await _financeService.UpsertEmployeeAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("payroll/adjustments")]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollAdjustmentViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPayrollAdjustments(
        Guid clinicId,
        [FromQuery] int? periodYear,
        [FromQuery] int? periodMonth,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.ListPayrollAdjustmentsAsync(
            userId, clinicId, periodYear, periodMonth, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("payroll/adjustments")]
    [ProducesResponseType(typeof(PayrollAdjustmentViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPayrollAdjustment(
        Guid clinicId,
        [FromBody] RecordPayrollAdjustmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RecordPayrollAdjustmentInput(
            dto.EmployeeId, dto.PeriodYear, dto.PeriodMonth, dto.AdjustmentType,
            dto.Amount, dto.Reason, dto.OccurredOnDate, dto.OperationId);
        var result = await _financeService.RecordPayrollAdjustmentAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("payroll/runs")]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollRunViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPayrollRuns(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.ListPayrollRunsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("payroll/runs")]
    [ProducesResponseType(typeof(PayrollRunViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePayrollRun(
        Guid clinicId,
        [FromBody] CreatePayrollRunDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreatePayrollRunInput(dto.PeriodYear, dto.PeriodMonth, dto.PaidFromMethod, dto.Notes, dto.OperationId);
        var result = await _financeService.CreatePayrollRunAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("payroll/runs/{payrollRunId:guid}/finalize")]
    [ProducesResponseType(typeof(PayrollRunViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> FinalizePayrollRun(
        Guid clinicId,
        Guid payrollRunId,
        [FromBody] FinalizePayrollRunDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _financeService.FinalizePayrollRunAsync(
            userId, clinicId, payrollRunId, dto?.OperationId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
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

        var result = await _financeService.GetIncomeVsExpensesAsync(userId, clinicId, fromDate, toDate, GetDeviceId(), cancellationToken);
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


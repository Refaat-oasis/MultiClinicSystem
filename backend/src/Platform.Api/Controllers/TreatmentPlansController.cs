using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 9 multi-session treatment plans, date windows,
/// delay recalculation, appointment booking, installment collection, and portal visibility toggle.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/treatment-plans")]
[Authorize]
public sealed class TreatmentPlansController : ControllerBase
{
    private readonly ITreatmentPlanService _treatmentPlanService;

    public TreatmentPlansController(ITreatmentPlanService treatmentPlanService)
    {
        _treatmentPlanService = treatmentPlanService ?? throw new ArgumentNullException(nameof(treatmentPlanService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TreatmentPlanViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlans(Guid clinicId, [FromQuery] Guid? patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.ListPlansAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(TreatmentPlanViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("{planId:guid}")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlan(Guid clinicId, Guid planId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.GetPlanAsync(userId, clinicId, planId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlan(
        Guid clinicId,
        [FromBody] CreateTreatmentPlanDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateTreatmentPlanInput(
            dto.Id,
            dto.PatientId,
            dto.DoctorId,
            dto.Name,
            dto.Mode,
            dto.FixedTotalAmount,
            dto.IncludedMaterialsCostAmount,
            dto.UpfrontAmount,
            dto.StartDate,
            dto.ShowCostToPatient,
            dto.Notes,
            dto.Sessions.Select(s => new CreatePlanSessionInput(
                s.SequenceNumber, s.Title, s.IntervalDaysFromPrevious, s.WindowMinusDays, s.WindowPlusDays,
                s.SessionPriceAmount, s.MaterialsCostAmount, s.Notes)).ToList(),
            dto.Installments.Select(i => new CreatePlanInstallmentInput(
                i.InstallmentNumber, i.IsUpfront, i.DueDate, i.Amount)).ToList(),
            dto.OperationId);

        var result = await _treatmentPlanService.CreatePlanAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{planId:guid}/sessions/{sessionId:guid}/suggest-slot")]
    [ProducesResponseType(typeof(PlanSuggestedSlotViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestOrBookSessionSlot(
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        [FromBody] SchedulePlanSessionDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        SchedulePlanSessionInput? input = dto != null
            ? new SchedulePlanSessionInput(dto.CategoryId, dto.RoomId, dto.PreferredStartUtc, dto.OperationId)
            : null;

        var result = await _treatmentPlanService.SuggestOrBookSessionAppointmentAsync(
            userId, clinicId, planId, sessionId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{planId:guid}/sessions/{sessionId:guid}/slip")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordSessionSlip(
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        [FromBody] RecordPlanSessionSlipDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.RecordSessionSlipAsync(
            userId, clinicId, planId, sessionId,
            new RecordPlanSessionSlipInput(dto.ActualOrSlippedDate, dto.Reason),
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{planId:guid}/sessions/{sessionId:guid}/complete")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteSession(
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        [FromBody] CompletePlanSessionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.CompletePlanSessionAsync(
            userId, clinicId, planId, sessionId,
            new CompletePlanSessionInput(dto.CompletedDate, dto.VisitId, dto.AdditionalMaterialsCostAmount, dto.OperationId),
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{planId:guid}/installments/{installmentId:guid}/pay")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayInstallment(
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        [FromBody] PayPlanInstallmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.PayInstallmentAsync(
            userId, clinicId, planId, installmentId,
            new PayPlanInstallmentInput(dto.Method, dto.Amount, dto.Notes, dto.OperationId),
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{planId:guid}/installments/{installmentId:guid}/refund")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefundInstallment(
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        [FromBody] RefundPlanDepositDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.RefundInstallmentAsync(
            userId, clinicId, planId, installmentId,
            new RefundPlanDepositInput(dto.Amount, dto.Reason, dto.OperationId),
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("{planId:guid}/portal-cost-visibility")]
    [ProducesResponseType(typeof(TreatmentPlanViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPortalCostVisibility(
        Guid clinicId,
        Guid planId,
        [FromBody] SetPlanPortalCostVisibilityDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _treatmentPlanService.SetPortalCostVisibilityAsync(
            userId, clinicId, planId, dto.ShowCostToPatient, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(TreatmentPlanViewModelMapper.ToViewModel(result.Value));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    private string? GetDeviceId() => User.FindFirstValue("device_id") ?? Request.Headers["X-Device-Id"].FirstOrDefault();
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.TraceIdentifier;
}


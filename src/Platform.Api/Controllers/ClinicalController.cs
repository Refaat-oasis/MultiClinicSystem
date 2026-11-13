using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for clinical visit sessions, vitals, diagnoses,
/// procedures, allergy-checked prescriptions + PDF, lab orders, specialty forms, and dental charting.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/clinical")]
[Authorize]
public sealed class ClinicalController : ControllerBase
{
    private readonly IClinicalService _clinicalService;

    public ClinicalController(IClinicalService clinicalService)
    {
        _clinicalService = clinicalService ?? throw new ArgumentNullException(nameof(clinicalService));
    }

    [HttpGet("icd10")]
    [ProducesResponseType(typeof(IReadOnlyList<Icd10CatalogEntryViewModel>), StatusCodes.Status200OK)]
    public IActionResult SearchIcd10(Guid clinicId, [FromQuery] string? query)
    {
        if (!TryGetUserId(out _))
        {
            return Unauthorized();
        }

        _ = clinicId;
        var entries = _clinicalService.SearchIcd10Catalog(query)
            .Select(ClinicalViewModelMapper.ToViewModel)
            .ToList();
        return Ok(entries);
    }

    [HttpPost("visits")]
    [ProducesResponseType(typeof(VisitSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartOrResumeVisit(
        Guid clinicId,
        [FromBody] StartVisitDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new StartVisitInput(
            dto.Id,
            dto.AppointmentId,
            dto.PatientId,
            dto.DoctorId,
            dto.SpecialtyCode,
            dto.RoomId,
            dto.PlannedDurationMinutes,
            dto.IsVisibleToPatient,
            dto.OperationId);

        var result = await _clinicalService.StartOrResumeVisitAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("visits/{visitId:guid}")]
    [ProducesResponseType(typeof(VisitSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisit(Guid clinicId, Guid visitId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.GetVisitAsync(userId, clinicId, visitId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("patients/{patientId:guid}/visits")]
    [ProducesResponseType(typeof(IReadOnlyList<VisitSessionViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPatientVisits(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.ListPatientVisitsAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(ClinicalViewModelMapper.ToViewModel).ToList());
    }

    [HttpPut("visits/{visitId:guid}/notes")]
    [ProducesResponseType(typeof(VisitSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotes(
        Guid clinicId,
        Guid visitId,
        [FromBody] UpdateVisitNotesDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpdateVisitNotesInput(
            dto.ChiefComplaint,
            dto.HistoryOfPresentIllness,
            dto.Examination,
            dto.DiagnosisSummary,
            dto.Advice,
            dto.PrivateDoctorNotes,
            dto.FollowUpDate,
            dto.IsVisibleToPatient);

        var result = await _clinicalService.UpdateVisitNotesAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/vitals")]
    [ProducesResponseType(typeof(VisitVitalViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordVitals(
        Guid clinicId,
        Guid visitId,
        [FromBody] RecordVisitVitalsDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RecordVisitVitalsInput(
            dto.BloodPressureSystolic,
            dto.BloodPressureDiastolic,
            dto.PulseBpm,
            dto.TemperatureCelsius,
            dto.WeightKg,
            dto.HeightCm,
            dto.SpO2Percent,
            dto.GlucoseMgDl);

        var result = await _clinicalService.RecordVitalsAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/diagnoses")]
    [ProducesResponseType(typeof(VisitDiagnosisViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDiagnosis(
        Guid clinicId,
        Guid visitId,
        [FromBody] AddVisitDiagnosisDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new AddVisitDiagnosisInput(dto.Icd10Code, dto.TitleAr, dto.TitleEn, dto.IsPrimary, dto.Notes);
        var result = await _clinicalService.AddDiagnosisAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/procedures")]
    [ProducesResponseType(typeof(VisitProcedureViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddProcedure(
        Guid clinicId,
        Guid visitId,
        [FromBody] AddVisitProcedureDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new AddVisitProcedureInput(dto.ProcedureCode, dto.NameAr, dto.NameEn, dto.ToothOrSite, dto.Notes);
        var result = await _clinicalService.AddProcedureAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/prescriptions")]
    [ProducesResponseType(typeof(PrescriptionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePrescription(
        Guid clinicId,
        Guid visitId,
        [FromBody] CreatePrescriptionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var items = (dto.Items ?? [])
            .Select(i => new PrescriptionItemInput(
                i.DrugName, i.ActiveIngredient, i.Dosage, i.Frequency, i.Duration, i.Instructions))
            .ToList();

        var input = new CreatePrescriptionInput(dto.Notes, dto.AcknowledgeAllergyWarning, items, dto.OperationId);
        var result = await _clinicalService.CreatePrescriptionAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("prescriptions/{prescriptionId:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrescriptionPdf(Guid clinicId, Guid prescriptionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.GetPrescriptionPdfAsync(
            userId, clinicId, prescriptionId, GetDeviceId(), cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.ContentBytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("visits/{visitId:guid}/lab-orders")]
    [ProducesResponseType(typeof(LabOrderViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLabOrder(
        Guid clinicId,
        Guid visitId,
        [FromBody] CreateLabOrderDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateLabOrderInput(dto.LabName, dto.TestOrWorkName, dto.ToothOrSite, dto.Instructions);
        var result = await _clinicalService.CreateLabOrderAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("lab-orders/{labOrderId:guid}/result")]
    [ProducesResponseType(typeof(LabOrderViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordLabResult(
        Guid clinicId,
        Guid labOrderId,
        [FromBody] RecordLabResultDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.RecordLabResultAsync(
            userId, clinicId, labOrderId, dto.ResultSummary, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("forms")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicalFormViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSpecialtyForms(
        Guid clinicId,
        [FromQuery] string? specialtyCode,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.ListSpecialtyFormsAsync(
            userId, clinicId, specialtyCode, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(ClinicalViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("forms")]
    [ProducesResponseType(typeof(ClinicalFormViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSpecialtyForm(
        Guid clinicId,
        [FromBody] CreateSpecialtyFormDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.CreateSpecialtyFormAsync(
            userId, clinicId, dto.SpecialtyCode, dto.TitleAr, dto.TitleEn, dto.JsonSchema,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/form-data")]
    [ProducesResponseType(typeof(VisitFormDataViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveVisitFormData(
        Guid clinicId,
        Guid visitId,
        [FromBody] SaveVisitFormDataDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new SaveVisitFormDataInput(dto.ClinicalFormId, dto.AnswersJson);
        var result = await _clinicalService.SaveVisitFormDataAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("visits/{visitId:guid}/dental-chart")]
    [ProducesResponseType(typeof(ToothRecordViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordToothState(
        Guid clinicId,
        Guid visitId,
        [FromBody] RecordToothStateDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RecordToothStateInput(dto.ToothCode, dto.NumberingSystem, dto.Condition, dto.Surfaces, dto.Notes);
        var result = await _clinicalService.RecordToothStateAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("patients/{patientId:guid}/dental-chart")]
    [ProducesResponseType(typeof(IReadOnlyList<DerivedToothStateViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientDentalChart(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _clinicalService.GetPatientDentalChartAsync(
            userId, clinicId, patientId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(ClinicalViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("visits/{visitId:guid}/complete")]
    [ProducesResponseType(typeof(VisitSessionViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteVisit(
        Guid clinicId,
        Guid visitId,
        [FromBody] CompleteVisitDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var toolsUsed = dto.ToolsUsed?
            .Select(t => new VisitToolUsageInput(t.InventoryItemId, t.Quantity, t.PatientUnitPriceOverride))
            .ToList();
        var input = new CompleteVisitInput(dto.IsOvertimeExcused, dto.OvertimeExcuseReason, dto.OperationId, toolsUsed);
        var result = await _clinicalService.CompleteVisitAsync(
            userId, clinicId, visitId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(ClinicalViewModelMapper.ToViewModel(result.Value));
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


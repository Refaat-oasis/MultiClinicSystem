using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller for clinic patient registry, Arabic search, duplicate detection, medical history, notes, and attachments.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/patients")]
[Authorize]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
    }

    [HttpGet("scoping")]
    [ProducesResponseType(typeof(ClinicPatientScopingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScoping(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.GetPatientScopingAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new ClinicPatientScopingViewModel(result.Value.ScopePatientsByDoctor, result.Value.ScopePatientsBySpecialty));
    }

    [HttpPut("scoping")]
    [ProducesResponseType(typeof(ClinicPatientScopingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfigureScoping(Guid clinicId, [FromBody] ConfigurePatientScopingDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.ConfigurePatientScopingAsync(
            userId, clinicId, dto.ScopePatientsByDoctor, dto.ScopePatientsBySpecialty,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new ClinicPatientScopingViewModel(result.Value.ScopePatientsByDoctor, result.Value.ScopePatientsBySpecialty));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PatientSearchViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPatients(Guid clinicId, [FromQuery] string? q, [FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.SearchPatientsAsync(userId, clinicId, q, limit, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new PatientSearchViewModel(
                result.Value.TotalCount,
                result.Value.Items.Select(PatientViewModelMapper.MapSummary).ToList()));
    }

    [HttpPost("check-duplicates")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientDuplicateWarningViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDuplicates(Guid clinicId, [FromBody] CheckPatientDuplicatesDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.CheckDuplicatesAsync(
            userId, clinicId, dto.FullName, dto.Phone, dto.ExcludePatientId, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(PatientViewModelMapper.MapWarning).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(PatientDetailViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePatient(Guid clinicId, [FromBody] CreatePatientDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreatePatientInput(
            dto.ClientPatientId,
            dto.FullName,
            dto.Phone,
            dto.SecondaryPhone,
            dto.DateOfBirth,
            dto.ApproximateAgeYears,
            dto.Gender,
            dto.NationalId,
            dto.Address,
            dto.AssignedDoctorId,
            dto.PrimarySpecialtyCode,
            dto.ConfirmDuplicateCreation,
            dto.OperationId);

        var result = await _patientService.CreatePatientAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/patients/{result.Value.Id}", PatientViewModelMapper.MapDetail(result.Value));
    }

    [HttpGet("{patientId:guid}")]
    [ProducesResponseType(typeof(PatientDetailViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatient(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.GetPatientAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientViewModelMapper.MapDetail(result.Value));
    }

    [HttpPut("{patientId:guid}")]
    [ProducesResponseType(typeof(PatientDetailViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePatient(Guid clinicId, Guid patientId, [FromBody] CreatePatientDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreatePatientInput(
            dto.ClientPatientId,
            dto.FullName,
            dto.Phone,
            dto.SecondaryPhone,
            dto.DateOfBirth,
            dto.ApproximateAgeYears,
            dto.Gender,
            dto.NationalId,
            dto.Address,
            dto.AssignedDoctorId,
            dto.PrimarySpecialtyCode,
            dto.ConfirmDuplicateCreation,
            dto.OperationId);

        var result = await _patientService.UpdatePatientAsync(
            userId, clinicId, patientId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientViewModelMapper.MapDetail(result.Value));
    }

    [HttpDelete("{patientId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidPatient(Guid clinicId, Guid patientId, [FromBody] VoidPatientItemDto? dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.VoidPatientAsync(
            userId, clinicId, patientId, dto?.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("Patient record archived."));
    }

    [HttpPost("{patientId:guid}/allergies")]
    [ProducesResponseType(typeof(PatientAllergyItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAllergy(Guid clinicId, Guid patientId, [FromBody] AddPatientAllergyDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.AddAllergyAsync(
            userId, clinicId, patientId, dto.Allergen, dto.Severity, dto.Reaction,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/patients/{patientId}/allergies/{result.Value.Id}", PatientViewModelMapper.MapAllergy(result.Value));
    }

    [HttpDelete("{patientId:guid}/allergies/{allergyId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidAllergy(Guid clinicId, Guid patientId, Guid allergyId, [FromBody] VoidPatientItemDto? dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.VoidAllergyAsync(
            userId, clinicId, patientId, allergyId, dto?.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("Allergy archived."));
    }

    [HttpPut("{patientId:guid}/history")]
    [ProducesResponseType(typeof(PatientDetailViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMedicalHistory(Guid clinicId, Guid patientId, [FromBody] UpdatePatientMedicalHistoryDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpdatePatientMedicalHistoryInput(
            dto.Summary,
            dto.ChronicConditions ?? Array.Empty<string>(),
            dto.CurrentMedications ?? Array.Empty<string>(),
            dto.PastSurgeries ?? Array.Empty<string>(),
            dto.MarkReviewed);

        var result = await _patientService.UpdateMedicalHistoryAsync(
            userId, clinicId, patientId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(PatientViewModelMapper.MapDetail(result.Value));
    }

    [HttpPost("{patientId:guid}/notes")]
    [ProducesResponseType(typeof(PatientNoteItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddNote(Guid clinicId, Guid patientId, [FromBody] AddPatientNoteDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.AddNoteAsync(
            userId, clinicId, patientId, dto.NoteText, dto.IsPrivate, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/patients/{patientId}/notes/{result.Value.Id}", PatientViewModelMapper.MapNote(result.Value));
    }

    [HttpDelete("{patientId:guid}/notes/{noteId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidNote(Guid clinicId, Guid patientId, Guid noteId, [FromBody] VoidPatientItemDto? dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.VoidNoteAsync(
            userId, clinicId, patientId, noteId, dto?.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("Note archived."));
    }

    [HttpPost("{patientId:guid}/attachments")]
    [ProducesResponseType(typeof(PatientAttachmentItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadAttachment(Guid clinicId, Guid patientId, [FromBody] UploadPatientAttachmentDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(dto.ContentBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            return ProblemDetailsMapper.ToProblemDetails(
                BuildingBlocks.Domain.Error.Validation("ATTACHMENT_INVALID_BASE64", "Attachment payload is not valid base64."));
        }

        var result = await _patientService.UploadAttachmentAsync(
            userId, clinicId, patientId, dto.Category, dto.FileName, dto.ContentType, rawBytes, dto.Notes,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/patients/{patientId}/attachments/{result.Value.Id}", PatientViewModelMapper.MapAttachment(result.Value));
    }

    [HttpGet("{patientId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid clinicId, Guid patientId, Guid attachmentId, [FromQuery] bool thumbnail = false, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.DownloadAttachmentAsync(
            userId, clinicId, patientId, attachmentId, thumbnail, GetDeviceId(), cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{patientId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidAttachment(Guid clinicId, Guid patientId, Guid attachmentId, [FromBody] VoidPatientItemDto? dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _patientService.VoidAttachmentAsync(
            userId, clinicId, patientId, attachmentId, dto?.Reason, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("Attachment archived."));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() => User.FindFirst("deviceId")?.Value ?? Request.Headers["X-Device-Id"].FirstOrDefault();
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.Items["RequestId"]?.ToString();
}


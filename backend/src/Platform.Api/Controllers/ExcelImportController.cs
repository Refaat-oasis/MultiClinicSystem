using System.Security.Claims;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 14 Excel (.xlsx) import templates, validation previews, and atomic commits.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/import")]
[Authorize]
public sealed class ExcelImportController : ControllerBase
{
    private readonly IExcelImportService _excelImportService;

    public ExcelImportController(IExcelImportService excelImportService)
    {
        _excelImportService = excelImportService ?? throw new ArgumentNullException(nameof(excelImportService));
    }

    [HttpGet("targets")]
    [ProducesResponseType(typeof(IReadOnlyList<ImportTargetKind>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTargets(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _excelImportService.ListAvailableTargetsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value);
    }

    [HttpGet("templates/{targetKind}")]
    public async Task<IActionResult> DownloadTemplate(Guid clinicId, ImportTargetKind targetKind, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _excelImportService.DownloadTemplateAsync(userId, clinicId, targetKind, GetDeviceId(), cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("{targetKind}/preview")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportValidationViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        Guid clinicId,
        ImportTargetKind targetKind,
        [FromForm] UploadExcelImportDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        byte[] content = await ReadFormFileBytesAsync(dto.File, cancellationToken);
        string fileName = dto.File?.FileName ?? $"{targetKind}.xlsx";
        var result = await _excelImportService.PreviewImportAsync(userId, clinicId, targetKind, fileName, content, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("{targetKind}/commit")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportValidationViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Commit(
        Guid clinicId,
        ImportTargetKind targetKind,
        [FromForm] UploadExcelImportDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        byte[] content = await ReadFormFileBytesAsync(dto.File, cancellationToken);
        string fileName = dto.File?.FileName ?? $"{targetKind}.xlsx";
        var result = await _excelImportService.CommitImportAsync(
            userId, clinicId, targetKind, fileName, content, dto.OperationId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(FinanceReportsViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<ImportBatchLogViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _excelImportService.ListImportHistoryAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(FinanceReportsViewModelMapper.ToViewModel).ToList());
    }

    private static async Task<byte[]> ReadFormFileBytesAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
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


using BuildingBlocks.Domain;
using Core.Domain;

namespace Core.Application;

public interface IExcelImportService
{
    Task<Result<IReadOnlyList<ImportTargetKind>>> ListAvailableTargetsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ImportTemplateResult>> DownloadTemplateAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ImportValidationResult>> PreviewImportAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        byte[] content,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ImportValidationResult>> CommitImportAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        byte[] content,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ImportBatchLogResult>>> ListImportHistoryAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);
}


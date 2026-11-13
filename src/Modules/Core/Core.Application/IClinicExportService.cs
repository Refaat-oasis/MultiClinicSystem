using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Exports a complete tenant-isolated clinic archive package on explicit owner request.
/// </summary>
public interface IClinicExportService
{
    Task<Result<ClinicExportPackageResult>> ExportClinicDataAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);
}


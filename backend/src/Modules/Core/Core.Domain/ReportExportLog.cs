using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Audit and widget metric log entry for generated report exports (Excel/PDF).
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class ReportExportLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public ReportExportKind ReportKind { get; private set; }
    public ReportExportFormat Format { get; private set; }
    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }
    public int RowCount { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public DateTimeOffset ExportedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ReportExportLog()
    {
    }

    public ReportExportLog(
        Guid id,
        Guid clinicId,
        ReportExportKind reportKind,
        ReportExportFormat format,
        DateOnly fromDate,
        DateOnly toDate,
        int rowCount,
        string fileName,
        string actorUserId,
        DateTimeOffset exportedAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        ReportKind = reportKind;
        Format = format;
        FromDate = fromDate;
        ToDate = toDate;
        RowCount = Math.Max(0, rowCount);
        FileName = fileName.Trim();
        ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        ExportedAtUtc = exportedAtUtc.ToUniversalTime();
    }
}


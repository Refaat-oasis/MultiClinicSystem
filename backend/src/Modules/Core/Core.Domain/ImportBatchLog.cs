using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Tracks Excel import dry-run previews and committed batches for audit & dashboard metrics.
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class ImportBatchLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public ImportTargetKind TargetKind { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public int TotalRows { get; private set; }
    public int AcceptedRows { get; private set; }
    public int RejectedRows { get; private set; }
    public bool IsCommitted { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public DateTimeOffset ExecutedAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ImportBatchLog()
    {
    }

    public ImportBatchLog(
        Guid id,
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        int totalRows,
        int acceptedRows,
        int rejectedRows,
        bool isCommitted,
        string actorUserId,
        DateTimeOffset executedAtUtc,
        Guid? operationId)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        TargetKind = targetKind;
        FileName = fileName.Trim();
        TotalRows = Math.Max(0, totalRows);
        AcceptedRows = Math.Max(0, acceptedRows);
        RejectedRows = Math.Max(0, rejectedRows);
        IsCommitted = isCommitted;
        ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        ExecutedAtUtc = executedAtUtc.ToUniversalTime();
        OperationId = operationId;
    }
}


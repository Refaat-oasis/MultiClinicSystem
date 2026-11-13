using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinic scheduled report preference used by report dashboards and automated export runs.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class ScheduledReportConfig : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public ReportExportKind ReportKind { get; private set; }
    public ReportExportFormat Format { get; private set; }
    public string Frequency { get; private set; } = "Monthly";
    public bool IsEnabled { get; private set; }
    public DateTimeOffset NextRunAtUtc { get; private set; }
    public DateTimeOffset? LastRunAtUtc { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ScheduledReportConfig()
    {
    }

    public ScheduledReportConfig(
        Guid id,
        Guid clinicId,
        ReportExportKind reportKind,
        ReportExportFormat format,
        string frequency,
        DateTimeOffset nextRunAtUtc,
        string createdByUserId,
        Guid? operationId)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        ReportKind = reportKind;
        Format = format;
        Frequency = string.IsNullOrWhiteSpace(frequency) ? "Monthly" : frequency.Trim();
        IsEnabled = true;
        NextRunAtUtc = nextRunAtUtc.ToUniversalTime();
        CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? "system" : createdByUserId.Trim();
        OperationId = operationId;
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}


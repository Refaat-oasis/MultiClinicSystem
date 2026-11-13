using Core.Domain;

namespace Platform.Api.DTOs;

public sealed class UpsertScheduledReportDto
{
    public ReportExportKind ReportKind { get; init; }
    public ReportExportFormat Format { get; init; }
    public string Frequency { get; init; } = "Monthly";
    public bool IsEnabled { get; init; } = true;
    public Guid? OperationId { get; init; }
}


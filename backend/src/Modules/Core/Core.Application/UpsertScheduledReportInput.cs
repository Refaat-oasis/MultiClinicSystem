using Core.Domain;

namespace Core.Application;

public sealed record UpsertScheduledReportInput(
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    string Frequency,
    bool IsEnabled,
    Guid? OperationId);


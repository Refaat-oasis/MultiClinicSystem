using Core.Domain;

namespace Core.Application;

public sealed record ScheduledReportConfigResult(
    Guid Id,
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    string Frequency,
    bool IsEnabled,
    DateTimeOffset NextRunAtUtc,
    DateTimeOffset? LastRunAtUtc);


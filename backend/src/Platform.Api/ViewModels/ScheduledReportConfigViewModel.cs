using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ScheduledReportConfigViewModel(
    Guid Id,
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    string Frequency,
    bool IsEnabled,
    DateTimeOffset NextRunAtUtc,
    DateTimeOffset? LastRunAtUtc);


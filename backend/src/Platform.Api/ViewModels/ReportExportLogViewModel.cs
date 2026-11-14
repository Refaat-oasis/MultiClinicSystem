using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ReportExportLogViewModel(
    Guid Id,
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowCount,
    string FileName,
    DateTimeOffset ExportedAtUtc);


using Core.Domain;

namespace Core.Application;

public sealed record ReportExportLogResult(
    Guid Id,
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    DateOnly FromDate,
    DateOnly ToDate,
    int RowCount,
    string FileName,
    DateTimeOffset ExportedAtUtc);


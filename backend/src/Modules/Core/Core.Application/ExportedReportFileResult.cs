using Core.Domain;

namespace Core.Application;

public sealed record ExportedReportFileResult(
    string FileName,
    string ContentType,
    byte[] Content,
    ReportExportKind ReportKind,
    ReportExportFormat Format,
    int RowCount);


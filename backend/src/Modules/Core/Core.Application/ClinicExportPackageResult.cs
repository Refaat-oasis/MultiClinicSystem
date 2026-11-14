namespace Core.Application;

/// <summary>
/// Full clinic data export archive result containing deterministic UTF-8 JSON payloads inside a ZIP package.
/// </summary>
public sealed record ClinicExportPackageResult(
    byte[] Content,
    string ContentType,
    string FileName,
    int RecordCount,
    DateTime ExportedAtUtc);


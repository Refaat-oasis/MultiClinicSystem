using Core.Domain;

namespace Core.Application;

public sealed record ImportValidationResult(
    ImportTargetKind TargetKind,
    string FileName,
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    bool CanCommit,
    bool Committed,
    IReadOnlyList<ImportRowErrorResult> Errors,
    IReadOnlyList<string> PreviewSummaries);


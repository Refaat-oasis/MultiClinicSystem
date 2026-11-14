using Core.Domain;

namespace Core.Application;

public sealed record ImportBatchLogResult(
    Guid Id,
    ImportTargetKind TargetKind,
    string FileName,
    int TotalRows,
    int AcceptedRows,
    int RejectedRows,
    bool IsCommitted,
    DateTimeOffset ExecutedAtUtc);


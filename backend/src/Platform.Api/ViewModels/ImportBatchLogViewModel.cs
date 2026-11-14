using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ImportBatchLogViewModel(
    Guid Id,
    ImportTargetKind TargetKind,
    string FileName,
    int TotalRows,
    int AcceptedRows,
    int RejectedRows,
    bool IsCommitted,
    DateTimeOffset ExecutedAtUtc);


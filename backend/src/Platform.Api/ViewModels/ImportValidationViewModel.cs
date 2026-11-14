using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ImportValidationViewModel(
    ImportTargetKind TargetKind,
    string FileName,
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    bool CanCommit,
    bool Committed,
    IReadOnlyList<ImportRowErrorViewModel> Errors,
    IReadOnlyList<string> PreviewSummaries);


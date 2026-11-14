namespace Platform.Api.ViewModels;

public sealed record VisitProcedureViewModel(
    Guid Id,
    Guid VisitId,
    string ProcedureCode,
    string NameAr,
    string NameEn,
    string? ToothOrSite,
    string? Notes,
    DateTimeOffset CreatedAtUtc);


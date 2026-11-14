namespace Core.Application;

/// <summary>
/// Read model for a clinical procedure recorded during a visit.
/// </summary>
public sealed record VisitProcedureResult(
    Guid Id,
    Guid VisitId,
    string ProcedureCode,
    string NameAr,
    string NameEn,
    string? ToothOrSite,
    string? Notes,
    DateTimeOffset CreatedAtUtc);


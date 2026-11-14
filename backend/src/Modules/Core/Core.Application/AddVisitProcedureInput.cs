namespace Core.Application;

/// <summary>
/// Input model to record a clinical procedure performed during a visit.
/// </summary>
public sealed record AddVisitProcedureInput(
    string ProcedureCode,
    string NameAr,
    string NameEn,
    string? ToothOrSite,
    string? Notes);


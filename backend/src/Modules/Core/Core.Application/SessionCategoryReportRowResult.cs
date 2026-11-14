namespace Core.Application;

public sealed record SessionCategoryReportRowResult(
    DateOnly VisitDate,
    Guid? CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    int SessionsCount,
    int CompletedSessionsCount,
    decimal BilledTotal);


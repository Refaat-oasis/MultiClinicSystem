namespace Platform.Api.ViewModels;

public sealed record SessionCategoryReportRowViewModel(
    DateOnly VisitDate,
    Guid? CategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    int SessionsCount,
    int CompletedSessionsCount,
    decimal BilledTotal);


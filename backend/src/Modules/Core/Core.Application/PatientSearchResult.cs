namespace Core.Application;

/// <summary>
/// Bounded patient search result containing matching patient summaries and total match count.
/// </summary>
public sealed record PatientSearchResult(
    int TotalCount,
    IReadOnlyList<PatientSummaryItemResult> Items);


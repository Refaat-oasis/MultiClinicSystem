namespace Core.Application;

/// <summary>
/// Aggregate-only dashboard widget response payload (never contains patient identifiers).
/// </summary>
public sealed record DashboardWidgetDataResult(
    string WidgetKey,
    string ModuleKey,
    string Scope,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string TitleAr,
    string TitleEn,
    decimal PrimaryValue,
    string UnitLabel,
    IReadOnlyDictionary<string, decimal> Breakdown);


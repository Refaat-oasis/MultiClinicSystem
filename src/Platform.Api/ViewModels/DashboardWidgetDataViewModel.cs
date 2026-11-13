namespace Platform.Api.ViewModels;

public sealed record DashboardWidgetDataViewModel(
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

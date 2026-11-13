namespace Core.Application;

/// <summary>
/// Combined widget definitions visible to the caller plus their saved pinned widget keys.
/// </summary>
public sealed record DashboardWidgetCatalogResult(
    Guid ClinicId,
    IReadOnlyList<DashboardWidgetDefinitionResult> Widgets,
    IReadOnlyList<string> PinnedWidgetKeys);

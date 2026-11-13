namespace Platform.Api.ViewModels;

public sealed record DashboardWidgetCatalogViewModel(
    Guid ClinicId,
    IReadOnlyList<DashboardWidgetDefinitionViewModel> Widgets,
    IReadOnlyList<string> PinnedWidgetKeys);

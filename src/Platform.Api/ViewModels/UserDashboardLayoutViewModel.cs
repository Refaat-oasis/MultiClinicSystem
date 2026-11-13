namespace Platform.Api.ViewModels;

public sealed record UserDashboardLayoutViewModel(
    Guid ClinicId,
    IReadOnlyList<string> PinnedWidgetKeys);

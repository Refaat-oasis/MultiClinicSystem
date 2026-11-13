namespace Platform.Api.DTOs;

public sealed class SaveDashboardLayoutDto
{
    public List<string> PinnedWidgetKeys { get; set; } = new();
}


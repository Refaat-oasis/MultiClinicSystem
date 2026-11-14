namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to create a lab or dental lab order for a visit.
/// </summary>
public sealed class CreateLabOrderDto
{
    public string LabName { get; set; } = string.Empty;
    public string TestOrWorkName { get; set; } = string.Empty;
    public string? ToothOrSite { get; set; }
    public string? Instructions { get; set; }
}


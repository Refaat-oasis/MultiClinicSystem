namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to record a tooth condition (FDI or Universal notation) in a dental visit.
/// </summary>
public sealed class RecordToothStateDto
{
    public string ToothCode { get; set; } = string.Empty;
    public string? NumberingSystem { get; set; }
    public string Condition { get; set; } = string.Empty;
    public List<string>? Surfaces { get; set; }
    public string? Notes { get; set; }
}


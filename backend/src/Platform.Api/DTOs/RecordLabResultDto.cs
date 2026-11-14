namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to record the result summary for a lab order.
/// </summary>
public sealed class RecordLabResultDto
{
    public string ResultSummary { get; set; } = string.Empty;
}


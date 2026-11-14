namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to mark or unmark a session duration as excused from statistical learning.
/// </summary>
public sealed class SetSessionDurationExcusedDto
{
    public bool IsExcused { get; set; }
    public string? ExcuseReason { get; set; }
    public Guid? OperationId { get; set; }
}


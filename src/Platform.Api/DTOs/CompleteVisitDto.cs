namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to complete a clinical visit session and emit SessionCompleted outbox event.
/// </summary>
public sealed class CompleteVisitDto
{
    public bool IsOvertimeExcused { get; set; }
    public string? OvertimeExcuseReason { get; set; }
    public Guid? OperationId { get; set; }
    public IReadOnlyList<VisitToolUsageDto>? ToolsUsed { get; set; }
}


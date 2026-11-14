namespace Core.Application;

/// <summary>
/// Input model to complete a clinical visit and publish the idempotent SessionCompleted event.
/// </summary>
public sealed record CompleteVisitInput(
    bool IsOvertimeExcused,
    string? OvertimeExcuseReason,
    Guid? OperationId,
    IReadOnlyList<VisitToolUsageInput>? ToolsUsed = null);


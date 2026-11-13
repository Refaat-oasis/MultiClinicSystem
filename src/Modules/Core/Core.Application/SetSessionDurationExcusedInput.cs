namespace Core.Application;

/// <summary>
/// Input model for marking or unmarking a session duration as excused from statistical learning.
/// </summary>
public sealed record SetSessionDurationExcusedInput(
    bool IsExcused,
    string? ExcuseReason,
    Guid? OperationId);


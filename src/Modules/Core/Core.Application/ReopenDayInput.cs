namespace Core.Application;

public sealed record ReopenDayInput(
    DateOnly ClosingDate,
    string Reason,
    Guid? OperationId);


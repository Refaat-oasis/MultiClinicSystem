namespace Core.Application;

public sealed record SchedulePlanSessionInput(
    Guid CategoryId,
    Guid? RoomId,
    DateTimeOffset? PreferredStartUtc,
    Guid? OperationId);


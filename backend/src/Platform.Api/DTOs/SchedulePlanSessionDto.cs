namespace Platform.Api.DTOs;

public sealed record SchedulePlanSessionDto(
    Guid CategoryId,
    Guid? RoomId,
    DateTimeOffset? PreferredStartUtc,
    Guid? OperationId);


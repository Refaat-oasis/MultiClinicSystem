namespace Core.Application;

public sealed record PortalAccessLogItemResult(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string ActorId,
    string ActorType,
    string Reason,
    string? Details);


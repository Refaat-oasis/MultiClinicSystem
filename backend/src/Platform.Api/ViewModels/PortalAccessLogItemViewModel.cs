namespace Platform.Api.ViewModels;

public sealed record PortalAccessLogItemViewModel(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string ActorId,
    string ActorType,
    string Reason,
    string? Details);


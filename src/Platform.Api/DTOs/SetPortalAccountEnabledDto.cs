namespace Platform.Api.DTOs;

public sealed record SetPortalAccountEnabledDto(
    bool Enabled,
    string? Reason);


namespace Core.Application;

public sealed record PortalSignedUrlResult(
    string Url,
    DateTimeOffset ExpiresAtUtc);


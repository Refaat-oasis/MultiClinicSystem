namespace Platform.Api.ViewModels;

public sealed record PortalSignedUrlViewModel(
    string Url,
    DateTimeOffset ExpiresAtUtc);


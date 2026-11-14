namespace Platform.Api.DTOs;

/// <summary>
/// HTTP request payload to allocate or refill a device's offline number block.
/// </summary>
public sealed record AllocateNumberBlockDto(
    string DeviceId,
    string Kind,
    int? RequestedSize = null);


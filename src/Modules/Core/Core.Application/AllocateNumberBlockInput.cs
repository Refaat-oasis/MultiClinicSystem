namespace Core.Application;

/// <summary>
/// Input model to pre-allocate or refill a device's offline receipt or patient-file number block.
/// </summary>
public sealed record AllocateNumberBlockInput(
    string DeviceId,
    string Kind,
    int? RequestedSize = null);


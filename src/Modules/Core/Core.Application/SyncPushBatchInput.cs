namespace Core.Application;

/// <summary>
/// Batch of offline mutations pushed by a clinic client device.
/// </summary>
public sealed record SyncPushBatchInput(
    string DeviceId,
    IReadOnlyList<SyncPushOperationInput> Operations);


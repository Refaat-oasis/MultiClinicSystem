namespace Platform.Api.DTOs;

/// <summary>
/// HTTP request payload for `/api/v1/clinics/{clinicId}/sync/push`.
/// </summary>
public sealed record SyncPushBatchDto(
    string DeviceId,
    IReadOnlyList<SyncPushOperationDto> Operations);


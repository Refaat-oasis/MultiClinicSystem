namespace Core.Application;

/// <summary>
/// Tenant-scoped real-time scheduling and queue event broadcast via SignalR.
/// Never carries patient names, phones, or medical details — only IDs and summary type.
/// </summary>
public sealed record ScheduleRealtimeEvent(
    Guid ClinicId,
    string EventType,
    Guid EntityId,
    Guid? DoctorId,
    DateTimeOffset OccurredAtUtc,
    string Summary);


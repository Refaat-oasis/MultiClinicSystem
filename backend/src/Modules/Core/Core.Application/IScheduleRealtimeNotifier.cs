namespace Core.Application;

/// <summary>
/// Publishes real-time scheduling and queue events strictly to clients connected to that clinic's SignalR group.
/// </summary>
public interface IScheduleRealtimeNotifier
{
    Task NotifyClinicAsync(ScheduleRealtimeEvent evt, CancellationToken cancellationToken = default);
}


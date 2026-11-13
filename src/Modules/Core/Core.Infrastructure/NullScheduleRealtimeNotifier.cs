using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// No-op realtime notifier used in non-HTTP direct service tests where SignalR HubContext is absent.
/// </summary>
public sealed class NullScheduleRealtimeNotifier : IScheduleRealtimeNotifier
{
    public Task NotifyClinicAsync(ScheduleRealtimeEvent evt, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}


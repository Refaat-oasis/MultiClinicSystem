using Core.Application;
using Microsoft.AspNetCore.SignalR;

namespace Platform.Api.Hubs;

/// <summary>
/// Broadcasts clinic scheduling events strictly to the matching clinic SignalR group.
/// </summary>
public sealed class SignalRScheduleRealtimeNotifier : IScheduleRealtimeNotifier
{
    private readonly IHubContext<ClinicScheduleHub> _hubContext;

    public SignalRScheduleRealtimeNotifier(IHubContext<ClinicScheduleHub> hubContext)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public Task NotifyClinicAsync(ScheduleRealtimeEvent evt, CancellationToken cancellationToken = default)
    {
        string groupName = ClinicScheduleHub.BuildGroupName(evt.ClinicId);
        return _hubContext.Clients.Group(groupName).SendAsync(evt.EventType, evt, cancellationToken);
    }
}


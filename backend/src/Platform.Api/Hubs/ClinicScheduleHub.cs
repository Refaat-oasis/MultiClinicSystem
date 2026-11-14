using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Platform.Api.Hubs;

/// <summary>
/// SignalR hub delivering tenant-isolated real-time scheduling and waiting-queue updates.
/// </summary>
[Authorize]
public sealed class ClinicScheduleHub : Hub
{
    private readonly IClinicAuthorizationService _authorizationService;

    public ClinicScheduleHub(IClinicAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public async Task JoinClinic(Guid clinicId)
    {
        string? rawUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(rawUserId, out Guid userId))
        {
            throw new HubException("UNAUTHORIZED");
        }

        string? deviceId = Context.User?.FindFirstValue("device_id");
        var gate = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            requiredPermission: "scheduling.read",
            requiredModuleKey: "scheduling",
            deviceId: deviceId,
            cancellationToken: Context.ConnectionAborted);

        if (gate.IsFailure)
        {
            throw new HubException(gate.Error.Code);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildGroupName(clinicId), Context.ConnectionAborted);
    }

    public Task LeaveClinic(Guid clinicId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildGroupName(clinicId), Context.ConnectionAborted);
    }

    public static string BuildGroupName(Guid clinicId) => $"clinic:{clinicId:N}";
}

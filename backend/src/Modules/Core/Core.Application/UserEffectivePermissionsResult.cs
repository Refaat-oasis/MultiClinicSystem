namespace Core.Application;

/// <summary>
/// Effective and dormant permissions resolved for a clinic user.
/// </summary>
public sealed record UserEffectivePermissionsResult(
    Guid ClinicId,
    Guid UserId,
    string RoleName,
    bool IsOwner,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> DormantPermissions,
    IReadOnlyList<string> EnabledModules);


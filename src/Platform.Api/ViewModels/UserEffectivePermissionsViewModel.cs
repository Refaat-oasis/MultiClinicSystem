namespace Platform.Api.ViewModels;

public sealed record UserEffectivePermissionsViewModel(
    Guid ClinicId,
    Guid UserId,
    string RoleName,
    bool IsOwner,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> DormantPermissions,
    IReadOnlyList<string> EnabledModules);


namespace Platform.Api.ViewModels;

public sealed record StaffMemberViewModel(
    Guid UserId,
    Guid ClinicId,
    string FullName,
    string Email,
    string RoleName,
    Guid? ClinicRoleId,
    bool IsOwner,
    bool IsActive,
    bool EmailConfirmed,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> DormantPermissions,
    IReadOnlyList<PermissionOverrideItemViewModel> Overrides);


namespace Core.Application;

/// <summary>
/// Read model for a staff member within a clinic along with their role, overrides, effective permissions, and devices.
/// </summary>
public sealed record StaffMemberResult(
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
    IReadOnlyList<PermissionOverrideInput> Overrides);


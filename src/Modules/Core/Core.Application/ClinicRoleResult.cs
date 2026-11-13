namespace Core.Application;

/// <summary>
/// Read model for a clinic-scoped role including assigned effective and dormant permissions.
/// </summary>
public sealed record ClinicRoleResult(
    Guid RoleId,
    Guid ClinicId,
    string Name,
    string NameAr,
    string NameEn,
    string? TemplateKey,
    bool IsSystemOwner,
    bool IsEditable,
    int AssignedUsersCount,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> DormantPermissions);

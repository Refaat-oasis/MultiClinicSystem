using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Manages clinic-scoped roles, role permissions, user permission overrides, staff invitations, device approvals, and effective permission checks.
/// </summary>
public interface IClinicAuthorizationService
{
    IReadOnlyList<PermissionDefinitionResult> GetPermissionCatalog();

    Task<Result<MyModulesManifestResult>> GetMyModulesManifestAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<UserEffectivePermissionsResult>> GetUserEffectivePermissionsAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureClinicAccessAsync(
        Guid userId,
        Guid clinicId,
        string? requiredPermission,
        string? requiredModuleKey,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicRoleResult>>> ListRolesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicRoleResult>> CreateRoleAsync(
        Guid userId,
        Guid clinicId,
        string name,
        string? nameAr,
        string? nameEn,
        IReadOnlyList<string> permissions,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicRoleResult>> CloneRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid sourceRoleId,
        string newName,
        string? newNameAr,
        string? newNameEn,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicRoleResult>> UpdateRolePermissionsAsync(
        Guid userId,
        Guid clinicId,
        Guid roleId,
        IReadOnlyList<string> permissions,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid roleId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StaffMemberResult>>> ListStaffAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResult>> InviteStaffAsync(
        Guid userId,
        Guid clinicId,
        string fullName,
        string email,
        string roleName,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResult>> AssignStaffRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        string roleName,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResult>> SetUserPermissionOverridesAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        IReadOnlyList<PermissionOverrideInput> overrides,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveStaffAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicDeviceItemResult>>> ListClinicDevicesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveClinicDeviceAsync(
        Guid userId,
        Guid clinicId,
        string targetDeviceId,
        string? callerDeviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);
}


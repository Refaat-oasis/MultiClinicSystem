namespace Platform.Api.ViewModels;

public sealed record ClinicRoleViewModel(
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

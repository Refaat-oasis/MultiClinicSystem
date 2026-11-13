namespace Core.Domain;

/// <summary>
/// Result of evaluating effective and dormant permissions for a user in a clinic.
/// </summary>
public sealed record EffectivePermissionsEvaluation(
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> DormantPermissions);

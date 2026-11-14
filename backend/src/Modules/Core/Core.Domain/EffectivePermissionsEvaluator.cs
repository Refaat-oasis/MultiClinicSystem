using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Pure domain evaluator for effective permissions, dormant permissions, reserved owner-only permissions, and delegation ceiling checks.
/// Formula: (RoleClaims U UserGrants \ UserDenies) intersect (Core U EnabledModulePermissions).
/// </summary>
public static class EffectivePermissionsEvaluator
{
    public static readonly IReadOnlySet<string> ReservedOwnerPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "clinic.settings.edit",
        "roles.manage",
        "clinic.export"
    };

    public static EffectivePermissionsEvaluation Evaluate(
        bool isOwner,
        IEnumerable<string> rolePermissions,
        IEnumerable<string> userGrants,
        IEnumerable<string> userDenies,
        IEnumerable<string> activeCatalogPermissions)
    {
        var activeSet = new HashSet<string>(activeCatalogPermissions, StringComparer.OrdinalIgnoreCase);
        var denySet = new HashSet<string>(userDenies, StringComparer.OrdinalIgnoreCase);

        if (isOwner)
        {
            var effectiveOwner = activeSet
                .Where(p => !denySet.Contains(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new EffectivePermissionsEvaluation(effectiveOwner, Array.Empty<string>());
        }

        var rawCandidate = new HashSet<string>(rolePermissions, StringComparer.OrdinalIgnoreCase);
        foreach (string grant in userGrants)
        {
            rawCandidate.Add(grant);
        }

        rawCandidate.ExceptWith(denySet);

        var effective = rawCandidate
            .Where(p => activeSet.Contains(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dormant = rawCandidate
            .Where(p => !activeSet.Contains(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EffectivePermissionsEvaluation(effective, dormant);
    }

    public static Result ValidateRolePermissionsAssignment(
        bool isSystemOwnerRole,
        IEnumerable<string> requestedPermissions,
        IReadOnlySet<string> allKnownPermissions,
        IReadOnlySet<string> actorEffectivePermissions)
    {
        if (isSystemOwnerRole)
        {
            return Result.Failure(Error.Forbidden("SYSTEM_ROLE_PROTECTED", "The system Owner role cannot be modified."));
        }

        foreach (string raw in requestedPermissions)
        {
            string perm = raw.Trim().ToLowerInvariant();
            if (!allKnownPermissions.Contains(perm))
            {
                return Result.Failure(Error.Validation("UNKNOWN_PERMISSION", $"Unknown permission '{perm}'."));
            }

            if (ReservedOwnerPermissions.Contains(perm))
            {
                return Result.Failure(Error.Forbidden(
                    "RESERVED_PERMISSION_FORBIDDEN",
                    $"Reserved owner permission '{perm}' cannot be assigned to a non-owner role."));
            }

            if (!actorEffectivePermissions.Contains(perm))
            {
                return Result.Failure(Error.Forbidden(
                    "DELEGATION_CEILING_EXCEEDED",
                    $"You cannot grant permission '{perm}' because you do not hold it."));
            }
        }

        return Result.Success();
    }

    public static Result ValidateUserGrant(
        string permissionKey,
        IReadOnlySet<string> allKnownPermissions,
        IReadOnlySet<string> actorEffectivePermissions,
        bool targetIsOwner)
    {
        string perm = permissionKey.Trim().ToLowerInvariant();
        if (!allKnownPermissions.Contains(perm))
        {
            return Result.Failure(Error.Validation("UNKNOWN_PERMISSION", $"Unknown permission '{perm}'."));
        }

        if (!targetIsOwner && ReservedOwnerPermissions.Contains(perm))
        {
            return Result.Failure(Error.Forbidden(
                "RESERVED_PERMISSION_FORBIDDEN",
                $"Reserved owner permission '{perm}' cannot be granted to a non-owner user."));
        }

        if (!actorEffectivePermissions.Contains(perm))
        {
            return Result.Failure(Error.Forbidden(
                "DELEGATION_CEILING_EXCEEDED",
                $"You cannot grant permission '{perm}' because you do not hold it."));
        }

        return Result.Success();
    }
}


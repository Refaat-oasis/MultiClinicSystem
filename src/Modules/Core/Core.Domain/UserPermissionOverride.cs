using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-user grant or deny permission override scoped to a specific clinic. Deny always wins over role claims and grants.
/// </summary>
public sealed class UserPermissionOverride : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionKey { get; private set; }
    public string Effect { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private UserPermissionOverride()
    {
        PermissionKey = string.Empty;
        Effect = "Grant";
    }

    public UserPermissionOverride(
        Guid id,
        Guid clinicId,
        Guid userId,
        string permissionKey,
        string effect,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Override ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            throw new ArgumentException("Permission key cannot be empty.", nameof(permissionKey));
        }

        string normalizedEffect = effect?.Trim() ?? string.Empty;
        if (!string.Equals(normalizedEffect, "Grant", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedEffect, "Deny", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Effect must be either 'Grant' or 'Deny'.", nameof(effect));
        }

        Id = id;
        ClinicId = clinicId;
        UserId = userId;
        PermissionKey = permissionKey.Trim().ToLowerInvariant();
        Effect = string.Equals(normalizedEffect, "Deny", StringComparison.OrdinalIgnoreCase) ? "Deny" : "Grant";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateEffect(string effect, Guid updatedByUserId, DateTimeOffset updatedAtUtc)
    {
        string normalizedEffect = effect?.Trim() ?? string.Empty;
        if (!string.Equals(normalizedEffect, "Grant", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedEffect, "Deny", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Effect must be either 'Grant' or 'Deny'.", nameof(effect));
        }

        Effect = string.Equals(normalizedEffect, "Deny", StringComparison.OrdinalIgnoreCase) ? "Deny" : "Grant";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Stores a permission claim granted to a clinic-scoped role.
/// </summary>
public sealed class ClinicRoleClaim : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid ClinicRoleId { get; private set; }
    public string PermissionKey { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicRoleClaim()
    {
        PermissionKey = string.Empty;
    }

    public ClinicRoleClaim(
        Guid id,
        Guid clinicId,
        Guid clinicRoleId,
        string permissionKey,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Claim ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (clinicRoleId == Guid.Empty)
        {
            throw new ArgumentException("ClinicRole ID cannot be empty.", nameof(clinicRoleId));
        }

        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            throw new ArgumentException("Permission key cannot be empty.", nameof(permissionKey));
        }

        Id = id;
        ClinicId = clinicId;
        ClinicRoleId = clinicRoleId;
        PermissionKey = permissionKey.Trim().ToLowerInvariant();
        CreatedAtUtc = createdAtUtc;
    }
}

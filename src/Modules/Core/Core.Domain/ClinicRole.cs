using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a clinic-scoped staff role (either a built-in template or a custom cloned role).
/// System Owner roles cannot be edited or deleted.
/// </summary>
public sealed class ClinicRole : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Name { get; private set; }
    public string NameAr { get; private set; }
    public string NameEn { get; private set; }
    public string? TemplateKey { get; private set; }
    public bool IsSystemOwner { get; private set; }
    public bool IsEditable { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicRole()
    {
        Name = string.Empty;
        NameAr = string.Empty;
        NameEn = string.Empty;
    }

    public ClinicRole(
        Guid id,
        Guid clinicId,
        string name,
        string nameAr,
        string nameEn,
        string? templateKey,
        bool isSystemOwner,
        bool isEditable,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Role ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name cannot be empty.", nameof(name));
        }

        Id = id;
        ClinicId = clinicId;
        Name = name.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? Name : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? Name : nameEn.Trim();
        TemplateKey = string.IsNullOrWhiteSpace(templateKey) ? null : templateKey.Trim();
        IsSystemOwner = isSystemOwner;
        IsEditable = !isSystemOwner && isEditable;
        CreatedAtUtc = createdAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        if (IsSystemOwner)
        {
            throw new InvalidOperationException("Cannot void system Owner role.");
        }

        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

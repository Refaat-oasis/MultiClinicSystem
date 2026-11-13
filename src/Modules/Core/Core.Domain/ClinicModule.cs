using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Tracks per-clinic activation state for each switchable platform module.
/// </summary>
public sealed class ClinicModule : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string ModuleKey { get; private set; }
    public bool Enabled { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }
    public string? Note { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicModule()
    {
        ModuleKey = string.Empty;
    }

    public ClinicModule(
        Guid id,
        Guid clinicId,
        string moduleKey,
        bool enabled,
        Guid changedByUserId,
        DateTimeOffset changedAtUtc,
        string? note = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("ClinicModule ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            throw new ArgumentException("Module key cannot be empty.", nameof(moduleKey));
        }

        Id = id;
        ClinicId = clinicId;
        ModuleKey = moduleKey.Trim().ToLowerInvariant();
        Enabled = enabled;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void SetEnabled(bool enabled, Guid changedByUserId, DateTimeOffset changedAtUtc, string? note = null)
    {
        Enabled = enabled;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
        if (note != null)
        {
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }
    }
}

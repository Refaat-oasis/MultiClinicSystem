using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Stores a user's customized dashboard widget layout for a clinic (LastWriterWins sync semantics).
/// </summary>
public sealed class UserDashboardLayout : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid UserId { get; private set; }
    public string PinnedWidgetsJson { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private UserDashboardLayout()
    {
        PinnedWidgetsJson = "[]";
    }

    public UserDashboardLayout(
        Guid id,
        Guid clinicId,
        Guid userId,
        string pinnedWidgetsJson,
        DateTimeOffset updatedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Layout ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        Id = id;
        ClinicId = clinicId;
        UserId = userId;
        PinnedWidgetsJson = string.IsNullOrWhiteSpace(pinnedWidgetsJson) ? "[]" : pinnedWidgetsJson;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateWidgets(string pinnedWidgetsJson, DateTimeOffset updatedAtUtc)
    {
        PinnedWidgetsJson = string.IsNullOrWhiteSpace(pinnedWidgetsJson) ? "[]" : pinnedWidgetsJson;
        UpdatedAtUtc = updatedAtUtc;
    }
}


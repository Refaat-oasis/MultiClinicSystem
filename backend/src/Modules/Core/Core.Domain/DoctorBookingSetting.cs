using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-doctor online booking configuration (defaults AcceptsOnlineBooking to false for Phase 11 portal hooks).
/// </summary>
public sealed class DoctorBookingSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid DoctorId { get; private set; }
    public bool AcceptsOnlineBooking { get; private set; }
    public int? MaxDailyOnlineBookings { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private DoctorBookingSetting()
    {
    }

    public DoctorBookingSetting(
        Guid id,
        Guid clinicId,
        Guid doctorId,
        bool acceptsOnlineBooking,
        int? maxDailyOnlineBookings,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Setting ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (doctorId == Guid.Empty)
        {
            throw new ArgumentException("Doctor ID cannot be empty.", nameof(doctorId));
        }

        Id = id;
        ClinicId = clinicId;
        DoctorId = doctorId;
        AcceptsOnlineBooking = acceptsOnlineBooking;
        MaxDailyOnlineBookings = maxDailyOnlineBookings is > 0 ? maxDailyOnlineBookings : null;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Update(bool acceptsOnlineBooking, int? maxDailyOnlineBookings, DateTimeOffset now)
    {
        AcceptsOnlineBooking = acceptsOnlineBooking;
        MaxDailyOnlineBookings = maxDailyOnlineBookings is > 0 ? maxDailyOnlineBookings : null;
        UpdatedAtUtc = now;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

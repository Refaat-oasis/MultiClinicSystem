using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Patient waiting-list request to receive freed-slot offers.
/// </summary>
public sealed class WaitingListEntry : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string PreferredDate { get; private set; }
    public string? Notes { get; private set; }
    public bool IsFulfilled { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private WaitingListEntry()
    {
        PreferredDate = string.Empty;
    }

    public WaitingListEntry(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? doctorId,
        Guid? categoryId,
        string preferredDate,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Waiting list ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(preferredDate))
        {
            throw new ArgumentException("Preferred date is required.", nameof(preferredDate));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        DoctorId = doctorId;
        CategoryId = categoryId;
        PreferredDate = preferredDate.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsFulfilled = false;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void MarkFulfilled(DateTimeOffset now)
    {
        IsFulfilled = true;
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

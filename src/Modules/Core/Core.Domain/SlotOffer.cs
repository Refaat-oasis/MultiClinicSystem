using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Offer created for a matching waiting-list patient whenever an occupied appointment slot is cancelled or freed.
/// </summary>
public sealed class SlotOffer : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid WaitingListEntryId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid CategoryId { get; private set; }
    public DateTimeOffset SlotStartUtc { get; private set; }
    public DateTimeOffset SlotEndUtc { get; private set; }
    public SlotOfferStatus Status { get; private set; }
    public DateTimeOffset OfferedAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SlotOffer()
    {
    }

    public SlotOffer(
        Guid id,
        Guid clinicId,
        Guid waitingListEntryId,
        Guid patientId,
        Guid doctorId,
        Guid? roomId,
        Guid categoryId,
        DateTimeOffset slotStartUtc,
        DateTimeOffset slotEndUtc,
        DateTimeOffset offeredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Offer ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (waitingListEntryId == Guid.Empty)
        {
            throw new ArgumentException("WaitingListEntry ID cannot be empty.", nameof(waitingListEntryId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (doctorId == Guid.Empty)
        {
            throw new ArgumentException("Doctor ID cannot be empty.", nameof(doctorId));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));
        }

        Id = id;
        ClinicId = clinicId;
        WaitingListEntryId = waitingListEntryId;
        PatientId = patientId;
        DoctorId = doctorId;
        RoomId = roomId;
        CategoryId = categoryId;
        SlotStartUtc = slotStartUtc;
        SlotEndUtc = slotEndUtc;
        Status = SlotOfferStatus.Pending;
        OfferedAtUtc = offeredAtUtc;
        CreatedAtUtc = offeredAtUtc;
    }

    public void Accept(DateTimeOffset now)
    {
        Status = SlotOfferStatus.Accepted;
        RespondedAtUtc = now;
    }

    public void Decline(DateTimeOffset now)
    {
        Status = SlotOfferStatus.Declined;
        RespondedAtUtc = now;
    }

    public void Expire(DateTimeOffset now)
    {
        Status = SlotOfferStatus.Expired;
        RespondedAtUtc = now;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

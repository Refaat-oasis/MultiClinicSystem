using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Live waiting queue entry supporting FIFO ordering, VIP jump tracking, and late push-back rules.
/// </summary>
public sealed class QueueEntry : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string QueueDate { get; private set; }
    public AppointmentPriority Priority { get; private set; }
    public int OrderIndex { get; private set; }
    public int OriginalPosition { get; private set; }
    public int VipJumpCount { get; private set; }
    public int PushCount { get; private set; }
    public QueueEntryStatus Status { get; private set; }
    public DateTimeOffset? ArrivedAtUtc { get; private set; }
    public DateTimeOffset? CalledAtUtc { get; private set; }
    public DateTimeOffset? ServedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private QueueEntry()
    {
        QueueDate = string.Empty;
    }

    public QueueEntry(
        Guid id,
        Guid clinicId,
        Guid? appointmentId,
        Guid patientId,
        Guid doctorId,
        string queueDate,
        AppointmentPriority priority,
        int orderIndex,
        int originalPosition,
        int vipJumpCount,
        QueueEntryStatus initialStatus,
        DateTimeOffset? arrivedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Queue entry ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (doctorId == Guid.Empty)
        {
            throw new ArgumentException("Doctor ID cannot be empty.", nameof(doctorId));
        }

        if (string.IsNullOrWhiteSpace(queueDate))
        {
            throw new ArgumentException("Queue date is required.", nameof(queueDate));
        }

        Id = id;
        ClinicId = clinicId;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        QueueDate = queueDate.Trim();
        Priority = priority;
        OrderIndex = orderIndex;
        OriginalPosition = originalPosition;
        VipJumpCount = Math.Max(0, vipJumpCount);
        PushCount = 0;
        Status = initialStatus;
        ArrivedAtUtc = arrivedAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void SetOrderIndex(int orderIndex, DateTimeOffset now)
    {
        OrderIndex = orderIndex;
        UpdatedAtUtc = now;
    }

    public void RecordArrival(DateTimeOffset arrivedAtUtc)
    {
        ArrivedAtUtc = arrivedAtUtc;
        if (Status == QueueEntryStatus.Late)
        {
            Status = QueueEntryStatus.Waiting;
        }

        UpdatedAtUtc = arrivedAtUtc;
    }

    public void IncrementPush(DateTimeOffset now, bool exceedMaxAsNoShow = false)
    {
        PushCount++;
        if (exceedMaxAsNoShow)
        {
            Status = QueueEntryStatus.NoShow;
        }

        UpdatedAtUtc = now;
    }

    public void MarkLate(DateTimeOffset now)
    {
        Status = QueueEntryStatus.Late;
        UpdatedAtUtc = now;
    }

    public void MarkInSession(DateTimeOffset now)
    {
        Status = QueueEntryStatus.InSession;
        CalledAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void MarkServed(DateTimeOffset now)
    {
        Status = QueueEntryStatus.Served;
        ServedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void MarkRemoved(DateTimeOffset now)
    {
        Status = QueueEntryStatus.Removed;
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

using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinic appointment entity supporting full status state machine, priority, push tracking, and channel source.
/// Conflict class: ServerValidated.
/// </summary>
public sealed class Appointment : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid CategoryId { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public AppointmentPriority Priority { get; private set; }
    public AppointmentSource Source { get; private set; }
    public int PushCount { get; private set; }
    public Guid? PlanSessionId { get; private set; }
    public string? Notes { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Appointment()
    {
    }

    public Appointment(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        Guid? roomId,
        Guid categoryId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        AppointmentPriority priority,
        AppointmentSource source,
        Guid? planSessionId,
        string? notes,
        Guid? createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Appointment ID cannot be empty.", nameof(id));
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

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));
        }

        if (endUtc <= startUtc)
        {
            throw new ArgumentException("EndUtc must be after StartUtc.", nameof(endUtc));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        DoctorId = doctorId;
        RoomId = roomId;
        CategoryId = categoryId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = AppointmentStatus.Booked;
        Priority = priority;
        Source = source;
        PushCount = 0;
        PlanSessionId = planSessionId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public bool TryTransitionTo(AppointmentStatus nextStatus, DateTimeOffset now, string? reason = null)
    {
        if (!AppointmentStateMachine.CanTransition(Status, nextStatus))
        {
            return false;
        }

        Status = nextStatus;
        if (nextStatus == AppointmentStatus.Cancelled && !string.IsNullOrWhiteSpace(reason))
        {
            CancelReason = reason.Trim();
        }

        UpdatedAtUtc = now;
        return true;
    }

    public void Reschedule(
        DateTimeOffset newStartUtc,
        DateTimeOffset newEndUtc,
        Guid? newDoctorId,
        Guid? newRoomId,
        DateTimeOffset now)
    {
        if (newEndUtc <= newStartUtc)
        {
            throw new ArgumentException("EndUtc must be after StartUtc.", nameof(newEndUtc));
        }

        if (newDoctorId.HasValue && newDoctorId.Value != Guid.Empty)
        {
            DoctorId = newDoctorId.Value;
        }

        RoomId = newRoomId;
        StartUtc = newStartUtc;
        EndUtc = newEndUtc;
        if (Status == AppointmentStatus.Late)
        {
            Status = AppointmentStatus.Booked;
        }

        UpdatedAtUtc = now;
    }

    public void SetPriority(AppointmentPriority priority, DateTimeOffset now)
    {
        Priority = priority;
        UpdatedAtUtc = now;
    }

    public int IncrementPushCount(DateTimeOffset now)
    {
        PushCount++;
        UpdatedAtUtc = now;
        return PushCount;
    }

    public void MarkLate(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Booked)
        {
            Status = AppointmentStatus.Late;
            UpdatedAtUtc = now;
        }
    }

    public void MarkNoShow(DateTimeOffset now)
    {
        if (Status is AppointmentStatus.Booked or AppointmentStatus.Late or AppointmentStatus.Arrived)
        {
            Status = AppointmentStatus.NoShow;
            UpdatedAtUtc = now;
        }
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

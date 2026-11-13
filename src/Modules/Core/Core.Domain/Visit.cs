using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinical session (visit) entity storing server-side session timing, overtime, SOAP notes, and portal visibility.
/// </summary>
public sealed class Visit : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid? AppointmentId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string SpecialtyCode { get; private set; } = string.Empty;
    public Guid? RoomId { get; private set; }
    public string? ChiefComplaint { get; private set; }
    public string? HistoryOfPresentIllness { get; private set; }
    public string? Examination { get; private set; }
    public string? DiagnosisSummary { get; private set; }
    public string? Advice { get; private set; }
    public string? PrivateDoctorNotes { get; private set; }
    public DateOnly? FollowUpDate { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public int PlannedDurationMinutes { get; private set; }
    public int OvertimeMinutes { get; private set; }
    public bool IsOvertimeExcused { get; private set; }
    public string? OvertimeExcuseReason { get; private set; }
    public VisitStatus Status { get; private set; } = VisitStatus.InProgress;
    public bool IsVisibleToPatient { get; private set; } = true;
    public bool IsVoided { get; private set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Visit()
    {
    }

    public Visit(
        Guid id,
        Guid clinicId,
        Guid? appointmentId,
        Guid patientId,
        Guid doctorId,
        string specialtyCode,
        Guid? roomId,
        int plannedDurationMinutes,
        DateTimeOffset startedAtUtc,
        bool isVisibleToPatient,
        Guid? operationId,
        DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (doctorId == Guid.Empty)
        {
            throw new ArgumentException("DoctorId is required.", nameof(doctorId));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? "general" : specialtyCode.Trim().ToLowerInvariant();
        RoomId = roomId;
        PlannedDurationMinutes = Math.Max(5, plannedDurationMinutes);
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        IsVisibleToPatient = isVisibleToPatient;
        OperationId = operationId;
        Status = VisitStatus.InProgress;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int ComputeElapsedMinutes(DateTimeOffset currentUtc)
    {
        DateTimeOffset end = EndedAtUtc ?? currentUtc.ToUniversalTime();
        return Math.Max(0, (int)Math.Floor((end - StartedAtUtc).TotalMinutes));
    }

    public int ComputeOvertimeMinutes(DateTimeOffset currentUtc)
    {
        return Math.Max(0, ComputeElapsedMinutes(currentUtc) - PlannedDurationMinutes);
    }

    public void UpdateClinicalNotes(
        string? chiefComplaint,
        string? historyOfPresentIllness,
        string? examination,
        string? diagnosisSummary,
        string? advice,
        string? privateDoctorNotes,
        DateOnly? followUpDate,
        bool isVisibleToPatient,
        DateTimeOffset updatedAtUtc)
    {
        ChiefComplaint = chiefComplaint?.Trim();
        HistoryOfPresentIllness = historyOfPresentIllness?.Trim();
        Examination = examination?.Trim();
        DiagnosisSummary = diagnosisSummary?.Trim();
        Advice = advice?.Trim();
        PrivateDoctorNotes = privateDoctorNotes?.Trim();
        FollowUpDate = followUpDate;
        IsVisibleToPatient = isVisibleToPatient;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public void Complete(DateTimeOffset endedAtUtc, bool isOvertimeExcused, string? overtimeExcuseReason, DateTimeOffset updatedAtUtc)
    {
        if (Status == VisitStatus.Completed)
        {
            throw new InvalidOperationException("Visit is already completed.");
        }

        DateTimeOffset utcEnd = endedAtUtc.ToUniversalTime();
        if (utcEnd < StartedAtUtc)
        {
            utcEnd = StartedAtUtc;
        }

        EndedAtUtc = utcEnd;
        OvertimeMinutes = ComputeOvertimeMinutes(utcEnd);
        IsOvertimeExcused = OvertimeMinutes > 0 && isOvertimeExcused;
        OvertimeExcuseReason = IsOvertimeExcused ? overtimeExcuseReason?.Trim() : null;
        Status = VisitStatus.Completed;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }
}


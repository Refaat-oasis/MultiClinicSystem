using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Weekly working hours for a clinic (DoctorId == null) or an individual doctor override.
/// </summary>
public sealed class ClinicWorkingHour : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public int DayOfWeek { get; private set; }
    public string StartTime { get; private set; }
    public string EndTime { get; private set; }
    public bool IsClosed { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicWorkingHour()
    {
        StartTime = "09:00";
        EndTime = "17:00";
    }

    public ClinicWorkingHour(
        Guid id,
        Guid clinicId,
        Guid? doctorId,
        int dayOfWeek,
        string startTime,
        string endTime,
        bool isClosed,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Working hour ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (dayOfWeek < 0 || dayOfWeek > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "DayOfWeek must be between 0 (Sunday) and 6 (Saturday).");
        }

        Id = id;
        ClinicId = clinicId;
        DoctorId = doctorId;
        DayOfWeek = dayOfWeek;
        StartTime = string.IsNullOrWhiteSpace(startTime) ? "09:00" : startTime.Trim();
        EndTime = string.IsNullOrWhiteSpace(endTime) ? "17:00" : endTime.Trim();
        IsClosed = isClosed;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(string startTime, string endTime, bool isClosed)
    {
        StartTime = string.IsNullOrWhiteSpace(startTime) ? "09:00" : startTime.Trim();
        EndTime = string.IsNullOrWhiteSpace(endTime) ? "17:00" : endTime.Trim();
        IsClosed = isClosed;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a clinic-wide or doctor-specific holiday or day-off date range.
/// </summary>
public sealed class ClinicDayOff : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public string StartDate { get; private set; }
    public string EndDate { get; private set; }
    public string ReasonAr { get; private set; }
    public string ReasonEn { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicDayOff()
    {
        StartDate = string.Empty;
        EndDate = string.Empty;
        ReasonAr = string.Empty;
        ReasonEn = string.Empty;
    }

    public ClinicDayOff(
        Guid id,
        Guid clinicId,
        Guid? doctorId,
        string startDate,
        string endDate,
        string reasonAr,
        string reasonEn,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("DayOff ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
        {
            throw new ArgumentException("Start and end dates are required.");
        }

        Id = id;
        ClinicId = clinicId;
        DoctorId = doctorId;
        StartDate = startDate.Trim();
        EndDate = endDate.Trim();
        ReasonAr = string.IsNullOrWhiteSpace(reasonAr) ? "إجازة" : reasonAr.Trim();
        ReasonEn = string.IsNullOrWhiteSpace(reasonEn) ? "Day Off" : reasonEn.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}

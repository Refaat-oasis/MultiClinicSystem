using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinical prescription header generated during a visit, carrying recorded allergy-check status.
/// </summary>
public sealed class Prescription : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid VisitId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string? Notes { get; private set; }
    public bool HasAllergyWarning { get; private set; }
    public string? AllergyWarningDetails { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Prescription()
    {
    }

    public Prescription(
        Guid id,
        Guid clinicId,
        Guid visitId,
        Guid patientId,
        Guid doctorId,
        string? notes,
        bool hasAllergyWarning,
        string? allergyWarningDetails,
        DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        VisitId = visitId;
        PatientId = patientId;
        DoctorId = doctorId;
        Notes = notes?.Trim();
        HasAllergyWarning = hasAllergyWarning;
        AllergyWarningDetails = allergyWarningDetails?.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Clinical or doctor-private note linked to a patient.
/// </summary>
public sealed class PatientNote : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? DoctorUserId { get; private set; }
    public string NoteText { get; private set; }
    public bool IsPrivate { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PatientNote()
    {
        NoteText = string.Empty;
    }

    public PatientNote(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? doctorUserId,
        string noteText,
        bool isPrivate,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Note ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(noteText))
        {
            throw new ArgumentException("Note text cannot be empty.", nameof(noteText));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        DoctorUserId = doctorUserId;
        NoteText = noteText.Trim();
        IsPrivate = isPrivate;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Recorded patient (or guardian) consent to clinic portal terms prior to or during activation.
/// </summary>
public sealed class PortalConsent : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? AcceptedByStaffUserId { get; private set; }
    public DateTimeOffset AcceptedAtUtc { get; private set; }
    public string TermsVersion { get; private set; }
    public string Channel { get; private set; }
    public string? Notes { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PortalConsent()
    {
        TermsVersion = "v1";
        Channel = "StaffRecorded";
    }

    public PortalConsent(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? acceptedByStaffUserId,
        DateTimeOffset acceptedAtUtc,
        string termsVersion,
        string channel,
        string? notes)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(termsVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        AcceptedByStaffUserId = acceptedByStaffUserId;
        AcceptedAtUtc = acceptedAtUtc.ToUniversalTime();
        TermsVersion = termsVersion.Trim();
        Channel = channel.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}


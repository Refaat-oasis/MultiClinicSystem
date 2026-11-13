using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Allergy warning entry attached to a patient inside a clinic tenant.
/// </summary>
public sealed class PatientAllergy : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public string Allergen { get; private set; }
    public string Severity { get; private set; }
    public string? Reaction { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PatientAllergy()
    {
        Allergen = string.Empty;
        Severity = "Moderate";
    }

    public PatientAllergy(
        Guid id,
        Guid clinicId,
        Guid patientId,
        string allergen,
        string severity,
        string? reaction,
        Guid recordedByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Allergy ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(allergen))
        {
            throw new ArgumentException("Allergen name cannot be empty.", nameof(allergen));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        Allergen = allergen.Trim();
        Severity = NormalizeSeverity(severity);
        Reaction = string.IsNullOrWhiteSpace(reaction) ? null : reaction.Trim();
        RecordedByUserId = recordedByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (string.Equals(severity, "Mild", StringComparison.OrdinalIgnoreCase))
        {
            return "Mild";
        }

        if (string.Equals(severity, "Severe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return "Severe";
        }

        return "Moderate";
    }
}


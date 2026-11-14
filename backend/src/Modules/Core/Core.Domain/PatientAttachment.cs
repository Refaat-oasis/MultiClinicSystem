using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Metadata for a patient image, lab report, scan, or document stored behind IFileStorage.
/// </summary>
public sealed class PatientAttachment : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public string Category { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public string Sha256Hash { get; private set; }
    public string StorageKey { get; private set; }
    public string? ThumbnailStorageKey { get; private set; }
    public string? Notes { get; private set; }
    public bool SharedWithPatient { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PatientAttachment()
    {
        Category = "Document";
        FileName = string.Empty;
        ContentType = string.Empty;
        Sha256Hash = string.Empty;
        StorageKey = string.Empty;
    }

    public PatientAttachment(
        Guid id,
        Guid clinicId,
        Guid patientId,
        string category,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256Hash,
        string storageKey,
        string? thumbnailStorageKey,
        string? notes,
        Guid uploadedByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Attachment ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));
        }

        Id = id;
        ClinicId = clinicId;
        PatientId = patientId;
        Category = string.IsNullOrWhiteSpace(category) ? "Document" : category.Trim();
        FileName = fileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        SizeBytes = sizeBytes;
        Sha256Hash = sha256Hash.Trim().ToLowerInvariant();
        StorageKey = storageKey.Trim();
        ThumbnailStorageKey = string.IsNullOrWhiteSpace(thumbnailStorageKey) ? null : thumbnailStorageKey.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SharedWithPatient = false;
        UploadedByUserId = uploadedByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetSharedWithPatient(bool shared)
    {
        SharedWithPatient = shared;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}


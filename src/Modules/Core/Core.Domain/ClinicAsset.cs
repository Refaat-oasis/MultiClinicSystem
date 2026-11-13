using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Metadata record for binary assets stored behind IFileStorage (such as clinic logos and user avatars).
/// </summary>
public sealed class ClinicAsset : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string OwnerType { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long OriginalSizeBytes { get; private set; }
    public string FullStorageKey { get; private set; }
    public string ThumbStorageKey { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicAsset()
    {
        OwnerType = string.Empty;
        FileName = string.Empty;
        ContentType = string.Empty;
        FullStorageKey = string.Empty;
        ThumbStorageKey = string.Empty;
    }

    public ClinicAsset(
        Guid id,
        Guid clinicId,
        string ownerType,
        string fileName,
        string contentType,
        long originalSizeBytes,
        string fullStorageKey,
        string thumbStorageKey,
        Guid uploadedByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Asset ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        Id = id;
        ClinicId = clinicId;
        OwnerType = ownerType.Trim();
        FileName = fileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        OriginalSizeBytes = originalSizeBytes;
        FullStorageKey = fullStorageKey;
        ThumbStorageKey = thumbStorageKey;
        UploadedByUserId = uploadedByUserId;
        CreatedAtUtc = createdAtUtc;
    }
}


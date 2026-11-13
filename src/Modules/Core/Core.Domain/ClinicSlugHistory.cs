namespace Core.Domain;

/// <summary>
/// Permanent history of clinic slugs so old public links never become reusable and always redirect.
/// </summary>
public sealed class ClinicSlugHistory
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Slug { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReplacedAtUtc { get; private set; }

    private ClinicSlugHistory()
    {
        Slug = string.Empty;
    }

    public ClinicSlugHistory(
        Guid id,
        Guid clinicId,
        string slug,
        bool isCurrent,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Slug history ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug cannot be empty.", nameof(slug));
        }

        Id = id;
        ClinicId = clinicId;
        Slug = slug.Trim().ToLowerInvariant();
        IsCurrent = isCurrent;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkReplaced(DateTimeOffset replacedAtUtc)
    {
        IsCurrent = false;
        ReplacedAtUtc = replacedAtUtc;
    }
}


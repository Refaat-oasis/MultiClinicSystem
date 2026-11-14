using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Sample tenant entity used to verify dual-layer tenant isolation (EF filter + SQL Server RLS).
/// </summary>
public sealed class TenantSampleRecord : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Title { get; private set; }
    public Money Amount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TenantSampleRecord()
    {
        Title = string.Empty;
    }

    public TenantSampleRecord(Guid id, Guid clinicId, string title, Money amount, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Record ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }

        Id = id;
        ClinicId = clinicId;
        Title = title.Trim();
        Amount = amount;
        CreatedAtUtc = createdAtUtc;
    }
}


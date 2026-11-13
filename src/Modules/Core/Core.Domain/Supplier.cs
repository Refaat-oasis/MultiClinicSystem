using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Finance-lite supplier or external lab account for tracking accrued costs and payments made.
/// </summary>
public sealed class Supplier : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SupplierType Type { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Supplier()
    {
    }

    public Supplier(
        Guid id,
        Guid clinicId,
        string name,
        SupplierType type,
        string? phone,
        string? notes,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.", nameof(name));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        Name = name.Trim();
        Type = type;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
        OperationId = operationId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
    }
}


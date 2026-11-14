using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Finance-lite expense entry (general operating expense, supplier invoice, or linked lab cost).
/// </summary>
public sealed class Expense : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PaymentMethod? Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public Guid? SupplierId { get; private set; }
    public Guid? LabWorkOrderId { get; private set; }
    public bool CountsAsCashOut { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Expense()
    {
    }

    public Expense(
        Guid id,
        Guid clinicId,
        string category,
        string description,
        PaymentMethod? method,
        Money amount,
        Guid? supplierId,
        Guid? labWorkOrderId,
        bool countsAsCashOut,
        string recordedByUserId,
        DateTimeOffset occurredAtUtc,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Expense category is required.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Expense description is required.", nameof(description));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Expense amount must be positive.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        Category = category.Trim();
        Description = description.Trim();
        Method = method;
        Amount = amount.Amount;
        Currency = amount.Currency;
        SupplierId = supplierId;
        LabWorkOrderId = labWorkOrderId;
        CountsAsCashOut = countsAsCashOut;
        RecordedByUserId = string.IsNullOrWhiteSpace(recordedByUserId) ? "system" : recordedByUserId.Trim();
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        OperationId = operationId;
    }
}


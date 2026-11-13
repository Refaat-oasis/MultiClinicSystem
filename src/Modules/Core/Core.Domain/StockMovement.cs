using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Append-only stock movement ledger row recording purchases, clinical session deductions, and manual adjustments.
/// Conflict class: AppendOnly.
/// </summary>
public sealed class StockMovement : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? BatchId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public decimal QuantityDelta { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public Guid? ReferenceVisitId { get; private set; }
    public Guid? ReferencePurchaseInvoiceId { get; private set; }
    public string? Reason { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public Guid? OperationId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private StockMovement()
    {
    }

    public StockMovement(
        Guid id,
        Guid clinicId,
        Guid itemId,
        Guid? batchId,
        StockMovementType movementType,
        decimal quantityDelta,
        decimal balanceAfter,
        Money unitCost,
        Guid? referenceVisitId,
        Guid? referencePurchaseInvoiceId,
        string? reason,
        string createdByUserId,
        Guid? operationId,
        DateTimeOffset occurredAtUtc)
    {
        if (clinicId == Guid.Empty || itemId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId and ItemId are required.");
        }

        if (quantityDelta == 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityDelta), "Movement delta cannot be zero.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        ItemId = itemId;
        BatchId = batchId;
        MovementType = movementType;
        QuantityDelta = decimal.Round(quantityDelta, 2, MidpointRounding.AwayFromZero);
        BalanceAfter = decimal.Round(balanceAfter, 2, MidpointRounding.AwayFromZero);
        UnitCostAmount = unitCost.Amount;
        Currency = unitCost.Currency;
        ReferenceVisitId = referenceVisitId;
        ReferencePurchaseInvoiceId = referencePurchaseInvoiceId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? "system" : createdByUserId.Trim();
        OperationId = operationId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }
}


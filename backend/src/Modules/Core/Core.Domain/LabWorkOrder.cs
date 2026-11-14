using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Lab work order linking a patient-facing Charge (patient price) and, when Finance is on, a lab Supplier Expense (lab cost).
/// Never exposes lab cost to the patient portal.
/// </summary>
public sealed class LabWorkOrder : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? VisitId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string LabName { get; private set; } = string.Empty;
    public string WorkDescription { get; private set; } = string.Empty;
    public string? ToothReference { get; private set; }
    public decimal LabCostAmount { get; private set; }
    public decimal PatientPriceAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public Guid? ChargeId { get; private set; }
    public Guid? ExpenseId { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private LabWorkOrder()
    {
    }

    public LabWorkOrder(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? visitId,
        Guid? doctorId,
        Guid? supplierId,
        string labName,
        string workDescription,
        string? toothReference,
        Money labCost,
        Money patientPrice,
        Guid? chargeId,
        Guid? expenseId,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(workDescription))
        {
            throw new ArgumentException("WorkDescription is required.", nameof(workDescription));
        }

        if (labCost.Amount < 0m || patientPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(patientPrice), "Amounts cannot be negative.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        VisitId = visitId;
        DoctorId = doctorId;
        SupplierId = supplierId;
        LabName = string.IsNullOrWhiteSpace(labName) ? "Dental Lab" : labName.Trim();
        WorkDescription = workDescription.Trim();
        ToothReference = string.IsNullOrWhiteSpace(toothReference) ? null : toothReference.Trim();
        LabCostAmount = labCost.Amount;
        PatientPriceAmount = patientPrice.Amount;
        Currency = patientPrice.Currency;
        ChargeId = chargeId;
        ExpenseId = expenseId;
        OperationId = operationId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void AttachExpense(Guid expenseId)
    {
        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException("ExpenseId cannot be empty.", nameof(expenseId));
        }

        ExpenseId = expenseId;
    }
}


using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Salaried employee roster record for clinic payroll (fixed salary only; no doctor commission).
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class Employee : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string RoleTitle { get; private set; } = string.Empty;
    public Guid? DoctorId { get; private set; }
    public string? Phone { get; private set; }
    public decimal BaseSalaryAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public PaymentMethod PaidFromMethod { get; private set; }
    public DateOnly HiredOnDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Employee()
    {
    }

    public Employee(
        Guid id,
        Guid clinicId,
        string fullName,
        string roleTitle,
        Guid? doctorId,
        string? phone,
        Money baseSalary,
        PaymentMethod paidFromMethod,
        DateOnly hiredOnDate,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Employee full name is required.", nameof(fullName));
        }

        if (baseSalary.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(baseSalary), "Base salary cannot be negative.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        FullName = fullName.Trim();
        RoleTitle = string.IsNullOrWhiteSpace(roleTitle) ? "Staff" : roleTitle.Trim();
        DoctorId = doctorId;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        BaseSalaryAmount = baseSalary.Amount;
        Currency = baseSalary.Currency;
        PaidFromMethod = paidFromMethod;
        HiredOnDate = hiredOnDate;
        IsActive = true;
        OperationId = operationId;
    }

    public void Update(
        string fullName,
        string roleTitle,
        Guid? doctorId,
        string? phone,
        Money baseSalary,
        PaymentMethod paidFromMethod,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Employee full name is required.", nameof(fullName));
        }

        if (baseSalary.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(baseSalary), "Base salary cannot be negative.");
        }

        FullName = fullName.Trim();
        RoleTitle = string.IsNullOrWhiteSpace(roleTitle) ? "Staff" : roleTitle.Trim();
        DoctorId = doctorId;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        BaseSalaryAmount = baseSalary.Amount;
        Currency = baseSalary.Currency;
        PaidFromMethod = paidFromMethod;
        IsActive = isActive;
    }
}


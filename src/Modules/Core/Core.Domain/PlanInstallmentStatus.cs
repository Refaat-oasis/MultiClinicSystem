namespace Core.Domain;

public enum PlanInstallmentStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Refunded = 5
}


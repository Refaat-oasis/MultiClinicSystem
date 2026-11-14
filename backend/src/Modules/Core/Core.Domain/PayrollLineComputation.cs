namespace Core.Domain;

/// <summary>
/// Pure computed payroll breakdown for a single employee in a period (no commission).
/// </summary>
public sealed record PayrollLineComputation(
    Guid EmployeeId,
    string EmployeeName,
    Guid? DoctorId,
    decimal BaseSalaryAmount,
    decimal BonusTotalAmount,
    decimal DeductionTotalAmount,
    decimal AdvanceTotalAmount,
    decimal NetPayAmount,
    string Currency);


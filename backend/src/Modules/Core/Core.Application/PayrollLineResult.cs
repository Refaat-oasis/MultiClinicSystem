namespace Core.Application;

public sealed record PayrollLineResult(
    Guid Id,
    Guid PayrollRunId,
    Guid EmployeeId,
    string EmployeeNameSnapshot,
    Guid? DoctorId,
    decimal BaseSalaryAmount,
    decimal BonusTotalAmount,
    decimal DeductionTotalAmount,
    decimal AdvanceTotalAmount,
    decimal NetPayAmount,
    string Currency);


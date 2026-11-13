namespace Platform.Api.ViewModels;

public sealed record PayrollLineViewModel(
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


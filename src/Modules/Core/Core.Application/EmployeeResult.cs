using Core.Domain;

namespace Core.Application;

public sealed record EmployeeResult(
    Guid Id,
    Guid ClinicId,
    string FullName,
    string RoleTitle,
    Guid? DoctorId,
    string? Phone,
    decimal BaseSalaryAmount,
    string Currency,
    PaymentMethod PaidFromMethod,
    DateOnly HiredOnDate,
    bool IsActive);


using Core.Domain;

namespace Core.Application;

public sealed record UpsertEmployeeInput(
    Guid? EmployeeId,
    string FullName,
    string RoleTitle,
    Guid? DoctorId,
    string? Phone,
    decimal BaseSalaryAmount,
    PaymentMethod PaidFromMethod,
    DateOnly HiredOnDate,
    bool IsActive,
    Guid? OperationId);


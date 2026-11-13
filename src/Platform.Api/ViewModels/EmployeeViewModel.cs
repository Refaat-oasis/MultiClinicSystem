using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record EmployeeViewModel(
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


using Core.Domain;

namespace Platform.Api.DTOs;

public sealed class UpsertEmployeeDto
{
    public Guid? EmployeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RoleTitle { get; init; } = string.Empty;
    public Guid? DoctorId { get; init; }
    public string? Phone { get; init; }
    public decimal BaseSalaryAmount { get; init; }
    public PaymentMethod PaidFromMethod { get; init; }
    public DateOnly HiredOnDate { get; init; }
    public bool IsActive { get; init; } = true;
    public Guid? OperationId { get; init; }
}


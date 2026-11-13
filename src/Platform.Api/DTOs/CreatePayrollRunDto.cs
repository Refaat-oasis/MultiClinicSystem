using Core.Domain;

namespace Platform.Api.DTOs;

public sealed class CreatePayrollRunDto
{
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public PaymentMethod PaidFromMethod { get; init; }
    public string? Notes { get; init; }
    public Guid? OperationId { get; init; }
}


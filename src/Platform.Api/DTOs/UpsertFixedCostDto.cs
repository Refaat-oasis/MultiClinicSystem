using Core.Domain;

namespace Platform.Api.DTOs;

public sealed class UpsertFixedCostDto
{
    public Guid? FixedCostId { get; init; }
    public FixedCostCategory Category { get; init; }
    public string TitleAr { get; init; } = string.Empty;
    public string TitleEn { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public FixedCostRecurrence Recurrence { get; init; }
    public PaymentMethod PaidFromMethod { get; init; }
    public DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
    public Guid? OperationId { get; init; }
}


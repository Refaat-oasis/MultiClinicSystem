using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record PayPlanInstallmentDto(
    PaymentMethod Method,
    decimal Amount,
    string? Notes,
    Guid? OperationId);


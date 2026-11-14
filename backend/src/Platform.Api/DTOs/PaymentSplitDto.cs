using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record PaymentSplitDto(
    PaymentMethod Method,
    decimal Amount,
    string? Notes);


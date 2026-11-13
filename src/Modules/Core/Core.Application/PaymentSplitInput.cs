using Core.Domain;

namespace Core.Application;

public sealed record PaymentSplitInput(
    PaymentMethod Method,
    decimal Amount,
    string? Notes);


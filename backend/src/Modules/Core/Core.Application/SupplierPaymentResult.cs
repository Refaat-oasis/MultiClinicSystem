using Core.Domain;

namespace Core.Application;

public sealed record SupplierPaymentResult(
    Guid Id,
    Guid ClinicId,
    Guid SupplierId,
    PaymentMethod Method,
    decimal Amount,
    string Currency,
    string? ReferenceNote,
    string PaidByUserId,
    DateTimeOffset PaidAtUtc);


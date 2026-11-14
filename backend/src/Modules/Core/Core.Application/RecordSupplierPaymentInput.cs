using Core.Domain;

namespace Core.Application;

public sealed record RecordSupplierPaymentInput(
    Guid? Id,
    Guid SupplierId,
    PaymentMethod Method,
    decimal Amount,
    string? ReferenceNote,
    Guid? OperationId);


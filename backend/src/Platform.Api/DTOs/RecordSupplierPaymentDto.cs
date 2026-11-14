using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record RecordSupplierPaymentDto(
    Guid? Id,
    Guid SupplierId,
    PaymentMethod Method,
    decimal Amount,
    string? ReferenceNote,
    Guid? OperationId);


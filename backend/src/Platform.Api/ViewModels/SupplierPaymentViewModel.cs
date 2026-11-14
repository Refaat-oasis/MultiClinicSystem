using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record SupplierPaymentViewModel(
    Guid Id,
    Guid ClinicId,
    Guid SupplierId,
    PaymentMethod Method,
    decimal Amount,
    string Currency,
    string? ReferenceNote,
    string PaidByUserId,
    DateTimeOffset PaidAtUtc);


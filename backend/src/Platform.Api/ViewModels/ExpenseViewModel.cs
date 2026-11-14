using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ExpenseViewModel(
    Guid Id,
    Guid ClinicId,
    string Category,
    string Description,
    PaymentMethod? Method,
    decimal Amount,
    string Currency,
    Guid? SupplierId,
    Guid? LabWorkOrderId,
    bool CountsAsCashOut,
    string RecordedByUserId,
    DateTimeOffset OccurredAtUtc);


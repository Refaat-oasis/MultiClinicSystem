using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record SupplierViewModel(
    Guid Id,
    Guid ClinicId,
    string Name,
    SupplierType Type,
    string? Phone,
    string? Notes,
    decimal TotalAccruedAmount,
    decimal TotalPaidAmount,
    decimal BalanceDueAmount,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);


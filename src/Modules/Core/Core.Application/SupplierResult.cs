using Core.Domain;

namespace Core.Application;

public sealed record SupplierResult(
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


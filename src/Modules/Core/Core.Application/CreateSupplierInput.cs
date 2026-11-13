using Core.Domain;

namespace Core.Application;

public sealed record CreateSupplierInput(
    Guid? Id,
    string Name,
    SupplierType Type,
    string? Phone,
    string? Notes,
    Guid? OperationId);


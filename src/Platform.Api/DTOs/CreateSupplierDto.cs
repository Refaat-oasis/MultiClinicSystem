using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CreateSupplierDto(
    Guid? Id,
    string Name,
    SupplierType Type,
    string? Phone,
    string? Notes,
    Guid? OperationId);


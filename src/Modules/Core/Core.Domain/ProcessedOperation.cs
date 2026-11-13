using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Records processed sync/client OperationId tokens per clinic for strict write idempotency.
/// </summary>
public sealed class ProcessedOperation : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid OperationId { get; private set; }
    public string EntityName { get; private set; }
    public Guid ResultEntityId { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ProcessedOperation()
    {
        EntityName = string.Empty;
    }

    public ProcessedOperation(
        Guid id,
        Guid clinicId,
        Guid operationId,
        string entityName,
        Guid resultEntityId,
        DateTimeOffset processedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("ProcessedOperation ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        Id = id;
        ClinicId = clinicId;
        OperationId = operationId;
        EntityName = string.IsNullOrWhiteSpace(entityName) ? "Unknown" : entityName.Trim();
        ResultEntityId = resultEntityId;
        ProcessedAtUtc = processedAtUtc;
    }
}


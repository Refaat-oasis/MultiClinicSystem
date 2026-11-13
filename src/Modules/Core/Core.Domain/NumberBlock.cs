using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Pre-allocated monotonic number block for offline-capable Receipt or PatientFile numbering without MAX()+1 races.
/// </summary>
public sealed class NumberBlock : ITenantEntity
{
    public const int DefaultBlockSize = 500;

    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string DeviceId { get; private set; }
    public string Kind { get; private set; }
    public long FromNumber { get; private set; }
    public long ToNumber { get; private set; }
    public long NextNumber { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private NumberBlock()
    {
        DeviceId = string.Empty;
        Kind = "Receipt";
        Status = "Active";
    }

    public NumberBlock(
        Guid id,
        Guid clinicId,
        string deviceId,
        string kind,
        long fromNumber,
        long toNumber,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("NumberBlock ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));
        }

        string normalizedKind = kind?.Trim() ?? string.Empty;
        if (!string.Equals(normalizedKind, "Receipt", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedKind, "PatientFile", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Kind must be 'Receipt' or 'PatientFile'.", nameof(kind));
        }

        if (fromNumber < 1 || toNumber < fromNumber)
        {
            throw new ArgumentException("Invalid number block bounds.");
        }

        Id = id;
        ClinicId = clinicId;
        DeviceId = deviceId.Trim();
        Kind = string.Equals(normalizedKind, "PatientFile", StringComparison.OrdinalIgnoreCase) ? "PatientFile" : "Receipt";
        FromNumber = fromNumber;
        ToNumber = toNumber;
        NextNumber = fromNumber;
        Status = "Active";
        CreatedAtUtc = createdAtUtc;
    }

    public Result<long> ConsumeNext()
    {
        if (string.Equals(Status, "Exhausted", StringComparison.OrdinalIgnoreCase) || NextNumber > ToNumber)
        {
            Status = "Exhausted";
            return Result<long>.Failure(Error.Conflict("NUMBER_BLOCK_EXHAUSTED", "Number block is exhausted."));
        }

        long allocated = NextNumber;
        NextNumber++;
        if (NextNumber > ToNumber)
        {
            Status = "Exhausted";
        }

        return Result<long>.Success(allocated);
    }
}

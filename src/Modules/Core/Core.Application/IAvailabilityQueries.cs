using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Pure availability query contract used by staff booking, portal booking, and "next free slot".
/// </summary>
public interface IAvailabilityQueries
{
    Task<Result<IReadOnlyList<FreeSlotItemResult>>> GetFreeSlotsAsync(
        Guid userId,
        Guid clinicId,
        Guid categoryId,
        Guid? doctorId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? deviceId,
        CancellationToken cancellationToken = default);
}


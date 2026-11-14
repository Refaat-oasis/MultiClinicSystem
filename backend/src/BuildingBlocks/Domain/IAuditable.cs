namespace BuildingBlocks.Domain;

/// <summary>
/// Tracks creation and modification audit timestamps and actor IDs.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? ModifiedAtUtc { get; set; }
    string? ModifiedBy { get; set; }
}


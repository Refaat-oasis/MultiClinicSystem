namespace BuildingBlocks.Domain;

/// <summary>
/// Designates an entity as supporting soft delete / voiding rather than hard deletion.
/// </summary>
public interface ISoftDeletable
{
    bool IsVoided { get; set; }
    DateTimeOffset? VoidedAtUtc { get; set; }
    string? VoidedBy { get; set; }
    string? VoidReason { get; set; }
}


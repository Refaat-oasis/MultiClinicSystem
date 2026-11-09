namespace BuildingBlocks.Application;

/// <summary>
/// Contract for queuing transactional outbox messages.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(string type, string content, CancellationToken cancellationToken = default);
}

namespace BuildingBlocks.Domain;

/// <summary>
/// Represents an outbox event or notification scheduled for reliable transactional delivery.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage()
    {
        Type = string.Empty;
        Content = string.Empty;
    }

    public OutboxMessage(Guid id, string type, string content, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Type = type ?? string.Empty;
        Content = content ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;
    }
}

namespace BuildingBlocks.Domain;

/// <summary>
/// Record representing an email message dispatched or captured during execution.
/// </summary>
public sealed class SentEmail
{
    public string To { get; }
    public string Subject { get; }
    public string Body { get; }
    public DateTimeOffset SentAtUtc { get; }

    public SentEmail(string to, string subject, string body, DateTimeOffset sentAtUtc)
    {
        To = to ?? string.Empty;
        Subject = subject ?? string.Empty;
        Body = body ?? string.Empty;
        SentAtUtc = sentAtUtc;
    }
}

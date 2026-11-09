using System.Collections.Concurrent;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// In-memory email sender implementation capturing dispatched messages for verification in tests.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sentMessages = new();

    public IReadOnlyList<SentEmail> SentMessages => _sentMessages.ToArray();

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var sentEmail = new SentEmail(to, subject, body, DateTimeOffset.UtcNow);
        _sentMessages.Enqueue(sentEmail);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _sentMessages.Clear();
    }
}

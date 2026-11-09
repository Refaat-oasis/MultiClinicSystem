using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements transactional outbox queuing and message dispatch.
/// </summary>
public sealed class OutboxService : IOutbox
{
    private readonly CoreDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEmailSender _emailSender;

    public OutboxService(CoreDbContext dbContext, IClock clock, IEmailSender emailSender)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task EnqueueAsync(string type, string content, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage(
            id: GuidV7.NewGuid(),
            type: type,
            content: content,
            createdAtUtc: _clock.UtcNow);

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Immediate dispatch when no outer uncommitted transaction is open
        if (_dbContext.Database.CurrentTransaction == null)
        {
            await ProcessPendingMessagesAsync(cancellationToken);
        }
    }

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                if (message.Type == "SendEmail")
                {
                    using var doc = JsonDocument.Parse(message.Content);
                    string to = doc.RootElement.GetProperty("to").GetString() ?? string.Empty;
                    string subject = doc.RootElement.GetProperty("subject").GetString() ?? string.Empty;
                    string body = doc.RootElement.GetProperty("body").GetString() ?? string.Empty;

                    await _emailSender.SendEmailAsync(to, subject, body, cancellationToken);
                }

                message.MarkProcessed(_clock.UtcNow);
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

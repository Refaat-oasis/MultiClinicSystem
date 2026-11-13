using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure;

/// <summary>
/// Implements transactional outbox queuing and message dispatch.
/// </summary>
public sealed class OutboxService : IOutbox
{
    private readonly CoreDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEmailSender _emailSender;
    private readonly IServiceProvider? _serviceProvider;

    public OutboxService(
        CoreDbContext dbContext,
        IClock clock,
        IEmailSender emailSender,
        IServiceProvider? serviceProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _serviceProvider = serviceProvider;
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
                else if (message.Type == "SessionCompleted" && _serviceProvider != null)
                {
                    using var doc = JsonDocument.Parse(message.Content);
                    if (doc.RootElement.TryGetProperty("VisitId", out JsonElement visitIdEl) &&
                        visitIdEl.TryGetGuid(out Guid visitId) &&
                        doc.RootElement.TryGetProperty("ClinicId", out JsonElement clinicIdEl) &&
                        clinicIdEl.TryGetGuid(out Guid clinicId) &&
                        doc.RootElement.TryGetProperty("ToolsUsed", out JsonElement toolsEl) &&
                        toolsEl.ValueKind == JsonValueKind.Array)
                    {
                        var toolsUsed = new List<VisitToolUsageInput>();
                        foreach (JsonElement itemEl in toolsEl.EnumerateArray())
                        {
                            if (itemEl.TryGetProperty("InventoryItemId", out JsonElement itemIdEl) &&
                                itemIdEl.TryGetGuid(out Guid itemId) &&
                                itemEl.TryGetProperty("Quantity", out JsonElement qtyEl) &&
                                qtyEl.TryGetDecimal(out decimal qty) &&
                                qty > 0m)
                            {
                                decimal? priceOverride = itemEl.TryGetProperty("PatientUnitPriceOverride", out JsonElement ovEl) &&
                                    ovEl.ValueKind == JsonValueKind.Number
                                    ? ovEl.GetDecimal()
                                    : null;
                                toolsUsed.Add(new VisitToolUsageInput(itemId, qty, priceOverride));
                            }
                        }

                        if (toolsUsed.Count > 0)
                        {
                            IInventoryService? inventoryService = _serviceProvider.GetService<IInventoryService>();
                            if (inventoryService != null)
                            {
                                await inventoryService.ConsumeVisitToolsAsync(
                                    clinicId, visitId, "outbox", toolsUsed, null, cancellationToken);
                            }
                        }
                    }
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

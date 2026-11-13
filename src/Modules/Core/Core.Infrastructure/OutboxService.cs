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
                        visitIdEl.ValueKind == JsonValueKind.String &&
                        visitIdEl.TryGetGuid(out Guid visitId) &&
                        doc.RootElement.TryGetProperty("ClinicId", out JsonElement clinicIdEl) &&
                        clinicIdEl.ValueKind == JsonValueKind.String &&
                        clinicIdEl.TryGetGuid(out Guid clinicId))
                    {
                        if (doc.RootElement.TryGetProperty("ToolsUsed", out JsonElement toolsEl) &&
                            toolsEl.ValueKind == JsonValueKind.Array)
                        {
                            var toolsUsed = new List<VisitToolUsageInput>();
                            foreach (JsonElement itemEl in toolsEl.EnumerateArray())
                            {
                                if (itemEl.TryGetProperty("InventoryItemId", out JsonElement itemIdEl) &&
                                    itemIdEl.ValueKind == JsonValueKind.String &&
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

                        if (doc.RootElement.TryGetProperty("DoctorId", out JsonElement doctorIdEl) &&
                            doctorIdEl.ValueKind == JsonValueKind.String &&
                            doctorIdEl.TryGetGuid(out Guid doctorId) &&
                            doc.RootElement.TryGetProperty("CategoryId", out JsonElement categoryIdEl) &&
                            categoryIdEl.ValueKind == JsonValueKind.String &&
                            categoryIdEl.TryGetGuid(out Guid categoryId) &&
                            categoryId != Guid.Empty)
                        {
                            int rawMinutes = doc.RootElement.TryGetProperty("RawMinutes", out JsonElement rawEl) && rawEl.ValueKind == JsonValueKind.Number && rawEl.TryGetInt32(out int rm) ? rm : 0;
                            int plannedMinutes = doc.RootElement.TryGetProperty("PlannedDurationMinutes", out JsonElement planEl) && planEl.ValueKind == JsonValueKind.Number && planEl.TryGetInt32(out int pm) ? pm : 0;
                            bool isExcused = doc.RootElement.TryGetProperty("IsOvertimeExcused", out JsonElement exEl) && exEl.ValueKind == JsonValueKind.True;
                            string? excuseReason = doc.RootElement.TryGetProperty("OvertimeExcuseReason", out JsonElement rsnEl) && rsnEl.ValueKind == JsonValueKind.String ? rsnEl.GetString() : null;
                            DateTimeOffset occurredAt = doc.RootElement.TryGetProperty("CompletedAtUtc", out JsonElement tsEl) && tsEl.ValueKind == JsonValueKind.String && tsEl.TryGetDateTimeOffset(out DateTimeOffset dt)
                                ? dt
                                : _clock.UtcNow;
                            Guid? operationId = doc.RootElement.TryGetProperty("OperationId", out JsonElement opEl) && opEl.ValueKind == JsonValueKind.String && opEl.TryGetGuid(out Guid opId) ? opId : null;

                            ITimeEngineService? timeEngineService = _serviceProvider.GetService<ITimeEngineService>();
                            if (timeEngineService != null)
                            {
                                await timeEngineService.ProcessCompletedSessionAsync(
                                    clinicId,
                                    visitId,
                                    doctorId,
                                    categoryId,
                                    rawMinutes,
                                    plannedMinutes,
                                    isExcused,
                                    excuseReason,
                                    occurredAt,
                                    operationId,
                                    cancellationToken);
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

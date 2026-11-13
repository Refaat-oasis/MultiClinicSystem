using System.Text.Json;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Internal workflow helper encapsulating waiting-queue insertion, VIP jump limit enforcement,
/// late push-back rules, waiting-list matching, and freed-slot offers.
/// </summary>
internal static class QueueWorkflowHelper
{
    public static async Task<bool> IsQueueModuleEnabledAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ClinicModules
            .AsNoTracking()
            .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "queue" && m.Enabled, cancellationToken);
    }

    public static async Task<QueueEntry> EnqueueInternalAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        Guid? appointmentId,
        Guid patientId,
        Guid doctorId,
        string queueDate,
        AppointmentPriority priority,
        int vipJumpLimit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<QueueEntry> waiting = await dbContext.QueueEntries
            .Where(q => q.ClinicId == clinicId
                        && !q.IsVoided
                        && q.QueueDate == queueDate
                        && q.DoctorId == doctorId
                        && q.Status == QueueEntryStatus.Waiting)
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        List<QueueReorderEntryInput> snapshots = waiting
            .Select(q => new QueueReorderEntryInput(q.Id, q.Priority, q.OrderIndex, q.CreatedAtUtc))
            .ToList();

        var (reordered, insertedOrder, originalTailPos, actualJumps) = QueueReorderCalculator.PlanInsertion(
            snapshots,
            priority,
            vipJumpLimit);

        Dictionary<Guid, int> newOrderLookup = reordered.ToDictionary(x => x.Id, x => x.NewOrderIndex);
        foreach (QueueEntry item in waiting)
        {
            if (newOrderLookup.TryGetValue(item.Id, out int updatedIdx) && item.OrderIndex != updatedIdx)
            {
                item.SetOrderIndex(updatedIdx, now);
            }
        }

        var entry = new QueueEntry(
            GuidV7.NewGuid(),
            clinicId,
            appointmentId,
            patientId,
            doctorId,
            queueDate,
            priority,
            insertedOrder,
            originalTailPos,
            actualJumps,
            QueueEntryStatus.Waiting,
            now,
            now);

        dbContext.QueueEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public static async Task<QueueEntryItemResult?> PushLateEntryAsync(
        CoreDbContext dbContext,
        IAuditService auditService,
        Guid clinicId,
        Guid userId,
        QueueEntry target,
        int maxQueuePushes,
        bool markNoShowOnExceed,
        string? ipAddress,
        string? requestId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (target.Status != QueueEntryStatus.Waiting)
        {
            return null;
        }

        List<QueueEntry> waiting = await dbContext.QueueEntries
            .Where(q => q.ClinicId == clinicId
                        && !q.IsVoided
                        && q.QueueDate == target.QueueDate
                        && q.DoctorId == target.DoctorId
                        && q.Status == QueueEntryStatus.Waiting)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);

        int nextPushCount = target.PushCount + 1;
        bool exceeded = nextPushCount > maxQueuePushes;

        if (!exceeded)
        {
            target.IncrementPush(now, exceedMaxAsNoShow: false);
            List<QueueReorderEntryInput> snapshots = waiting
                .Select(q => new QueueReorderEntryInput(q.Id, q.Priority, q.OrderIndex, q.CreatedAtUtc))
                .ToList();

            IReadOnlyList<QueueReorderOutputItem> reordered = QueueReorderCalculator.PlanPushBackOnePlace(snapshots, target.Id);
            Dictionary<Guid, int> newOrders = reordered.ToDictionary(x => x.Id, x => x.NewOrderIndex);
            foreach (QueueEntry w in waiting)
            {
                if (newOrders.TryGetValue(w.Id, out int idx) && w.OrderIndex != idx)
                {
                    w.SetOrderIndex(idx, now);
                }
            }
        }
        else
        {
            if (markNoShowOnExceed)
            {
                target.IncrementPush(now, exceedMaxAsNoShow: true);
            }
            else
            {
                target.IncrementPush(now, exceedMaxAsNoShow: false);
                target.MarkLate(now);
            }

            List<QueueEntry> remaining = waiting.Where(w => w.Id != target.Id).OrderBy(w => w.OrderIndex).ToList();
            for (int i = 0; i < remaining.Count; i++)
            {
                remaining[i].SetOrderIndex(i + 1, now);
            }
        }

        if (target.AppointmentId.HasValue)
        {
            Appointment? linked = await dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == target.AppointmentId.Value, cancellationToken);
            if (linked != null)
            {
                linked.IncrementPushCount(now);
                if (exceeded)
                {
                    if (markNoShowOnExceed)
                    {
                        linked.MarkNoShow(now);
                        await PortalBookingHelper.RecordPatientNoShowAsync(dbContext, clinicId, linked.PatientId, now, cancellationToken);
                    }
                    else
                    {
                        linked.MarkLate(now);
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            "scheduling.queue.push_late",
            nameof(QueueEntry),
            target.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { target.PushCount, Status = target.Status.ToString(), Exceeded = exceeded }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        List<QueueEntryItemResult> hydrated = await SchedulingProjectionHelper.HydrateQueueAsync(
            dbContext,
            clinicId,
            [target],
            cancellationToken);
        return hydrated.FirstOrDefault();
    }

    public static async Task<SlotOffer?> TryCreateOfferForFreedSlotAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        Appointment cancelledAppointment,
        string localDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        WaitingListEntry? match = await dbContext.WaitingListEntries
            .Where(w => w.ClinicId == clinicId
                        && !w.IsVoided
                        && !w.IsFulfilled
                        && w.PreferredDate == localDate
                        && (!w.DoctorId.HasValue || w.DoctorId == cancelledAppointment.DoctorId)
                        && (!w.CategoryId.HasValue || w.CategoryId == cancelledAppointment.CategoryId))
            .OrderBy(w => w.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (match == null)
        {
            return null;
        }

        var offer = new SlotOffer(
            GuidV7.NewGuid(),
            clinicId,
            match.Id,
            match.PatientId,
            cancelledAppointment.DoctorId,
            cancelledAppointment.RoomId,
            cancelledAppointment.CategoryId,
            cancelledAppointment.StartUtc,
            cancelledAppointment.EndUtc,
            now);

        dbContext.SlotOffers.Add(offer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return offer;
    }
}

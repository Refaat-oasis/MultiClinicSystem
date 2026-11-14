namespace Core.Domain;

/// <summary>
/// Pure queue ordering domain service implementing FIFO, VIP jump limit, Rush emergency jump, and push-back.
/// </summary>
public static class QueueReorderCalculator
{
    public static (IReadOnlyList<QueueReorderOutputItem> ReorderedExisting, int InsertedOrderIndex, int OriginalTailPosition, int ActualJumps) PlanInsertion(
        IReadOnlyList<QueueReorderEntryInput> waitingEntries,
        AppointmentPriority incomingPriority,
        int vipJumpLimit)
    {
        List<QueueReorderEntryInput> ordered = waitingEntries
            .OrderBy(e => e.OrderIndex)
            .ThenBy(e => e.CreatedAtUtc)
            .ThenBy(e => e.Id)
            .ToList();

        int tailPosition = ordered.Count + 1;
        if (ordered.Count == 0 || incomingPriority == AppointmentPriority.Normal)
        {
            List<QueueReorderOutputItem> unchanged = ordered
                .Select((e, idx) => new QueueReorderOutputItem(e.Id, idx + 1))
                .ToList();
            return (unchanged, tailPosition, tailPosition, 0);
        }

        int targetZeroBasedIndex;
        int actualJumps;

        if (incomingPriority == AppointmentPriority.Rush)
        {
            // Jump ahead of all non-Rush waiting entries, FIFO behind existing Rush entries.
            int afterLastRush = 0;
            while (afterLastRush < ordered.Count && ordered[afterLastRush].Priority == AppointmentPriority.Rush)
            {
                afterLastRush++;
            }

            targetZeroBasedIndex = afterLastRush;
            actualJumps = ordered.Count - targetZeroBasedIndex;
        }
        else
        {
            // VIP: jump ahead of up to vipJumpLimit trailing Normal entries; never pass Rush or earlier VIP entries.
            int clampedLimit = Math.Max(0, vipJumpLimit);
            int minAllowedIndex = 0;
            while (minAllowedIndex < ordered.Count && ordered[minAllowedIndex].Priority != AppointmentPriority.Normal)
            {
                minAllowedIndex++;
            }

            int maxJumpableNormal = ordered.Count - minAllowedIndex;
            actualJumps = Math.Min(clampedLimit, maxJumpableNormal);
            targetZeroBasedIndex = ordered.Count - actualJumps;
        }

        var updated = new List<QueueReorderOutputItem>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            int newOrder = i < targetZeroBasedIndex ? i + 1 : i + 2;
            updated.Add(new QueueReorderOutputItem(ordered[i].Id, newOrder));
        }

        return (updated, targetZeroBasedIndex + 1, tailPosition, actualJumps);
    }

    public static IReadOnlyList<QueueReorderOutputItem> PlanPushBackOnePlace(
        IReadOnlyList<QueueReorderEntryInput> waitingEntries,
        Guid targetEntryId)
    {
        List<QueueReorderEntryInput> ordered = waitingEntries
            .OrderBy(e => e.OrderIndex)
            .ThenBy(e => e.CreatedAtUtc)
            .ThenBy(e => e.Id)
            .ToList();

        int idx = ordered.FindIndex(e => e.Id == targetEntryId);
        if (idx >= 0 && idx < ordered.Count - 1)
        {
            (ordered[idx], ordered[idx + 1]) = (ordered[idx + 1], ordered[idx]);
        }

        return ordered
            .Select((e, i) => new QueueReorderOutputItem(e.Id, i + 1))
            .ToList();
    }
}


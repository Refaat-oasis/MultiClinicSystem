namespace Core.Domain;

/// <summary>
/// Pure domain allocation and pricing helpers for inventory pack purchases, markup pricing, and FEFO consumption.
/// </summary>
public static class InventoryFefoAllocator
{
    public sealed record BatchSlice(Guid BatchId, DateOnly? ExpiryDate, decimal QuantityRemaining, decimal UnitCost);

    public sealed record AllocatedBatchDeduction(Guid BatchId, decimal DeductedQuantity, decimal UnitCost, decimal LineCost);

    public sealed record FefoAllocationResult(
        IReadOnlyList<AllocatedBatchDeduction> BatchDeductions,
        decimal UnallocatedShortfallQuantity,
        decimal TotalCost,
        decimal WeightedAverageUnitCost,
        bool WentNegative);

    public static decimal CalculatePackUnitCost(decimal packPriceAmount, int packSize, int packCount)
    {
        if (packPriceAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(packPriceAmount), "Pack price cannot be negative.");
        }

        if (packSize <= 0 || packCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packSize), "Pack size and pack count must be positive.");
        }

        int totalUnits = checked(packSize * packCount);
        decimal totalSpend = packPriceAmount * packCount;
        return decimal.Round(totalSpend / totalUnits, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateMarkedUpPatientPrice(decimal unitCost, decimal markupPercent)
    {
        if (unitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");
        }

        if (markupPercent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(markupPercent), "Markup percent cannot be negative.");
        }

        return decimal.Round(unitCost * (1m + (markupPercent / 100m)), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Allocates requested consumption quantity across non-expired batches ordered by earliest expiry first (null expiry last).
    /// </summary>
    public static FefoAllocationResult Allocate(
        decimal requestedQuantity,
        IReadOnlyList<BatchSlice> candidateBatches,
        DateOnly todayLocal,
        decimal fallbackUnitCost,
        NegativeStockPolicy policy)
    {
        if (requestedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Requested quantity must be positive.");
        }

        var ordered = candidateBatches
            .Where(b => b.QuantityRemaining > 0m && (!b.ExpiryDate.HasValue || b.ExpiryDate.Value >= todayLocal))
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchId)
            .ToList();

        decimal availableTotal = ordered.Sum(b => b.QuantityRemaining);
        decimal remainingToDeduct = requestedQuantity;
        var deductions = new List<AllocatedBatchDeduction>();
        decimal totalCost = 0m;

        foreach (BatchSlice batch in ordered)
        {
            if (remainingToDeduct <= 0m)
            {
                break;
            }

            decimal take = Math.Min(batch.QuantityRemaining, remainingToDeduct);
            decimal lineCost = decimal.Round(take * batch.UnitCost, 2, MidpointRounding.AwayFromZero);
            deductions.Add(new AllocatedBatchDeduction(batch.BatchId, take, batch.UnitCost, lineCost));
            totalCost += lineCost;
            remainingToDeduct -= take;
        }

        if (remainingToDeduct > 0m && policy == NegativeStockPolicy.Block)
        {
            throw new InvalidOperationException(
                $"INSUFFICIENT_STOCK: Requested {requestedQuantity}, available {availableTotal}.");
        }

        decimal shortfall = remainingToDeduct;
        if (shortfall > 0m)
        {
            totalCost += decimal.Round(shortfall * Math.Max(0m, fallbackUnitCost), 2, MidpointRounding.AwayFromZero);
        }

        decimal weightedAvg = requestedQuantity > 0m
            ? decimal.Round(totalCost / requestedQuantity, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new FefoAllocationResult(
            deductions,
            shortfall,
            decimal.Round(totalCost, 2, MidpointRounding.AwayFromZero),
            weightedAvg,
            shortfall > 0m);
    }
}


using BuildingBlocks.Domain;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class InventoryDomainTests
{
    [Fact]
    public void Given_BoxPackPurchase_When_CalculatingUnitCostAndMarkup_Then_RoundsAwayFromZeroAccurately()
    {
        decimal unitCost = InventoryFefoAllocator.CalculatePackUnitCost(packPriceAmount: 100m, packSize: 30, packCount: 1);
        unitCost.Should().Be(3.33m);

        decimal editedPackCost = InventoryFefoAllocator.CalculatePackUnitCost(packPriceAmount: 150m, packSize: 25, packCount: 2);
        editedPackCost.Should().Be(6.00m);

        decimal patientPrice = InventoryFefoAllocator.CalculateMarkedUpPatientPrice(unitCost: 6.00m, markupPercent: 50m);
        patientPrice.Should().Be(9.00m);
    }

    [Fact]
    public void Given_MultipleBatchesWithExpiries_When_AllocatingFefo_Then_ExpiredBatchesAreSkippedAndEarliestValidIsConsumedFirst()
    {
        DateOnly today = new(2026, 9, 1);
        Guid expiredBatchId = GuidV7.NewGuid();
        Guid soonBatchId = GuidV7.NewGuid();
        Guid laterBatchId = GuidV7.NewGuid();
        Guid noExpiryBatchId = GuidV7.NewGuid();

        var result = InventoryFefoAllocator.Allocate(
            requestedQuantity: 7m,
            candidateBatches:
            [
                new InventoryFefoAllocator.BatchSlice(expiredBatchId, new DateOnly(2026, 8, 30), 10m, 4.00m),
                new InventoryFefoAllocator.BatchSlice(laterBatchId, new DateOnly(2026, 12, 1), 10m, 5.50m),
                new InventoryFefoAllocator.BatchSlice(soonBatchId, new DateOnly(2026, 9, 15), 5m, 5.00m),
                new InventoryFefoAllocator.BatchSlice(noExpiryBatchId, null, 10m, 6.00m)
            ],
            todayLocal: today,
            fallbackUnitCost: 5.00m,
            policy: NegativeStockPolicy.Block);

        result.WentNegative.Should().BeFalse();
        result.BatchDeductions.Should().HaveCount(2);
        result.BatchDeductions[0].BatchId.Should().Be(soonBatchId);
        result.BatchDeductions[0].DeductedQuantity.Should().Be(5m);
        result.BatchDeductions[1].BatchId.Should().Be(laterBatchId);
        result.BatchDeductions[1].DeductedQuantity.Should().Be(2m);
        result.TotalCost.Should().Be(36.00m);
        result.WeightedAverageUnitCost.Should().Be(5.14m);
    }

    [Fact]
    public void Given_InsufficientStock_When_AllocatingUnderBlockVsAllowPolicy_Then_BlockThrowsAndAllowTracksNegativeShortfall()
    {
        DateOnly today = new(2026, 9, 1);
        var slices = new List<InventoryFefoAllocator.BatchSlice>
        {
            new(GuidV7.NewGuid(), new DateOnly(2026, 10, 1), 2m, 10.00m)
        };

        Action blocked = () => InventoryFefoAllocator.Allocate(
            5m, slices, today, 10.00m, NegativeStockPolicy.Block);
        blocked.Should().Throw<InvalidOperationException>().WithMessage("*INSUFFICIENT_STOCK*");

        var allowed = InventoryFefoAllocator.Allocate(
            5m, slices, today, 10.00m, NegativeStockPolicy.AllowWithWarning);
        allowed.WentNegative.Should().BeTrue();
        allowed.UnallocatedShortfallQuantity.Should().Be(3m);
        allowed.TotalCost.Should().Be(50.00m);
    }

    [Fact]
    public void Given_PurchaseLineAndItem_When_ReceivingEditedPack_Then_ItemQuantityAndUnitCostUpdateConsistently()
    {
        DateTimeOffset now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        Guid clinicId = GuidV7.NewGuid();
        var item = new InventoryItem(
            GuidV7.NewGuid(), clinicId, "GLV-01", "قفازات طبية", "Medical Gloves",
            "pair", 30, new Money(4.00m), 25m, 10m, "dental", "Cabinet A", null, now);

        var line = new PurchaseInvoiceLine(
            GuidV7.NewGuid(), clinicId, GuidV7.NewGuid(), item.Id, GuidV7.NewGuid(),
            packSize: 20, packCount: 2, new Money(120.00m), "B-2026-01", new DateOnly(2026, 11, 1));

        line.TotalUnits.Should().Be(40m);
        line.UnitCostAmount.Should().Be(6.00m);

        item.ReceivePurchaseUnits(line.TotalUnits, line.UnitCostAmount, line.PackSize, now);
        item.QuantityOnHand.Should().Be(40m);
        item.PackSize.Should().Be(20);
        item.UnitCostAmount.Should().Be(6.00m);
        item.GetPatientMarkedUpPrice().Amount.Should().Be(7.50m);
    }
}


using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class TenantIsolationTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public TenantIsolationTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_RecordCreatedForClinicA_When_QueriedByClinicB_Then_RecordIsNotReturned()
    {
        // Arrange
        var clinicAId = GuidV7.NewGuid();
        var clinicBId = GuidV7.NewGuid();
        var recordId = GuidV7.NewGuid();

        var contextA = new CurrentClinicContext();
        contextA.SetTenant(clinicAId);

        await using (var dbA = _fixture.CreateDbContext(contextA))
        {
            var record = new TenantSampleRecord(
                id: recordId,
                clinicId: clinicAId,
                title: "Confidential Clinic A Record",
                amount: new Money(150.00m, "EGP"),
                createdAtUtc: DateTimeOffset.UtcNow
            );

            dbA.TenantSampleRecords.Add(record);
            await dbA.SaveChangesAsync();
        }

        // Act - Query as Clinic B
        var contextB = new CurrentClinicContext();
        contextB.SetTenant(clinicBId);

        TenantSampleRecord? foundRecord;
        List<TenantSampleRecord> listRecords;

        await using (var dbB = _fixture.CreateDbContext(contextB))
        {
            foundRecord = await dbB.TenantSampleRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            listRecords = await dbB.TenantSampleRecords.ToListAsync();
        }

        // Assert - Isolation enforced: Clinic B sees null and 0 records
        foundRecord.Should().BeNull();
        listRecords.Should().NotContain(r => r.Id == recordId);
    }

    [Fact]
    public async Task Given_RecordCreatedForClinicA_When_QueriedByClinicA_Then_RecordIsReturned()
    {
        // Arrange
        var clinicAId = GuidV7.NewGuid();
        var recordId = GuidV7.NewGuid();

        var contextA = new CurrentClinicContext();
        contextA.SetTenant(clinicAId);

        await using (var dbA = _fixture.CreateDbContext(contextA))
        {
            var record = new TenantSampleRecord(
                id: recordId,
                clinicId: clinicAId,
                title: "Clinic A Patient Invoice",
                amount: new Money(350.50m, "EGP"),
                createdAtUtc: DateTimeOffset.UtcNow
            );

            dbA.TenantSampleRecords.Add(record);
            await dbA.SaveChangesAsync();
        }

        // Act & Assert
        await using (var dbA = _fixture.CreateDbContext(contextA))
        {
            var found = await dbA.TenantSampleRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            found.Should().NotBeNull();
            found!.Title.Should().Be("Clinic A Patient Invoice");
            found.Amount.Amount.Should().Be(350.50m);
        }
    }

    [Fact]
    public async Task Given_RecordCreatedForClinicA_When_PlatformAdminBypasses_Then_RecordIsVisible()
    {
        // Arrange
        var clinicAId = GuidV7.NewGuid();
        var recordId = GuidV7.NewGuid();

        var context = new CurrentClinicContext();
        context.SetTenant(clinicAId);

        await using (var db = _fixture.CreateDbContext(context))
        {
            var record = new TenantSampleRecord(
                id: recordId,
                clinicId: clinicAId,
                title: "Audited Record",
                amount: new Money(500.00m, "EGP"),
                createdAtUtc: DateTimeOffset.UtcNow
            );

            db.TenantSampleRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // Act - Platform Admin bypass
        var platformAccess = new PlatformAccessService(context);

        TenantSampleRecord? accessedRecord = await platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                await using var dbAdmin = _fixture.CreateDbContext(context);
                return await dbAdmin.TenantSampleRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            },
            reason: "Super Admin Compliance Investigation",
            actorId: "super_admin_ahmed"
        );

        // Assert
        accessedRecord.Should().NotBeNull();
        accessedRecord!.Id.Should().Be(recordId);
        accessedRecord.ClinicId.Should().Be(clinicAId);
    }
}


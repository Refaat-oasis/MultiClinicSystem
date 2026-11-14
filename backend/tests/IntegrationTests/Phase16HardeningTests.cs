using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class Phase16HardeningTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase16HardeningTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_ClinicDataExportRlsAndIndexes_When_AuditedAndQueried_Then_EnforcesIsolationZeroSecretsAndLatencyBudget()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero)
        };
        var currentClinic = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(currentClinic, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var exportService = sp.GetRequiredService<IClinicExportService>();
        var dashboard = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);

        // Two distinct owners for strict cross-tenant export & RLS checks
        string ownerAEmail = $"p16-a-{Guid.NewGuid():N}@clinic.local";
        var ownerARes = await adminService.CreateOwnerAsync(admin.Id, "Owner A", ownerAEmail, null, 2, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerAEmail, "OwnerPass#2026");
        Guid ownerAId = ownerARes.Value.UserId;

        string ownerBEmail = $"p16-b-{Guid.NewGuid():N}@clinic.local";
        var ownerBRes = await adminService.CreateOwnerAsync(admin.Id, "Owner B", ownerBEmail, null, 2, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerBEmail, "OwnerPass#2026");
        Guid ownerBId = ownerBRes.Value.UserId;

        const string deviceId = "dev-activate";
        Guid clinicA = (await clinicsService.CreateClinicAsync(ownerAId, "عيادة أ ١٦", "UTC", "EGP", "None", deviceId, null, null)).Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerAId, clinicA, $"p16-a-{clinicA:N}"[..16], ["dental"]);
        (await adminService.UpdateClinicModulesAsync(admin.Id, clinicA, [new ModuleToggleInput("patients", true, "P16")], "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();

        Guid clinicB = (await clinicsService.CreateClinicAsync(ownerBId, "عيادة ب ١٦", "UTC", "EGP", "None", deviceId, null, null)).Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerBId, clinicB, $"p16-b-{clinicB:N}"[..16], ["dental"]);
        (await adminService.UpdateClinicModulesAsync(admin.Id, clinicB, [new ModuleToggleInput("patients", true, "P16")], "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();

        var patARes = await patientService.CreatePatientAsync(
            ownerAId, clinicA,
            new CreatePatientInput(null, "مريض تصدير أ", "01055550001", null, null, 30, "Female", null, null, null, "dental", false, null),
            deviceId, null, null);
        patARes.IsSuccess.Should().BeTrue();

        var patBRes = await patientService.CreatePatientAsync(
            ownerBId, clinicB,
            new CreatePatientInput(null, "مريض معزول ب", "01055550002", null, null, 40, "Male", null, null, null, "dental", false, null),
            deviceId, null, null);
        patBRes.IsSuccess.Should().BeTrue();

        // 1. Cross-tenant export blocked with NotFound (404 policy)
        var crossExport = await exportService.ExportClinicDataAsync(ownerBId, clinicA, deviceId, null, null);
        crossExport.IsFailure.Should().BeTrue();
        crossExport.Error.Code.Should().Be("CLINIC_NOT_FOUND");

        // 2. Owner A exports Clinic A archive -> valid ZIP containing Patient A and ZERO Patient B / passwords / secrets
        var exportRes = await exportService.ExportClinicDataAsync(ownerAId, clinicA, deviceId, "127.0.0.1", "req-export-16");
        exportRes.IsSuccess.Should().BeTrue();
        exportRes.Value.ContentType.Should().Be("application/zip");
        exportRes.Value.RecordCount.Should().BeGreaterThanOrEqualTo(1);

        using (var ms = new MemoryStream(exportRes.Value.Content))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            zip.Entries.Select(e => e.FullName).Should().Contain(["manifest.json", "clinic.json", "patients.json", "payments.json"]);
            using var reader = new StreamReader(zip.GetEntry("patients.json")!.Open(), Encoding.UTF8);
            string patientsJson = await reader.ReadToEndAsync();
            patientsJson.Should().Contain("مريض تصدير أ");
            patientsJson.Should().NotContain("مريض معزول ب");
            patientsJson.Should().NotContain("PasswordHash");
        }

        // Verify ClinicDataExported audit log was written
        var auditWritten = await db.AuditLogs.AnyAsync(a => a.ClinicId == clinicA && a.Action == "ClinicDataExported");
        auditWritten.Should().BeTrue();

        // 3. Verify SQL Server indexed ClinicId columns across tenant tables
        await using (var sqlConn = new Microsoft.Data.SqlClient.SqlConnection(db.Database.GetConnectionString()))
        {
            await sqlConn.OpenAsync();
            await using var cmd = sqlConn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(DISTINCT t.object_id)
                FROM sys.tables t
                INNER JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'ClinicId'
                WHERE EXISTS (
                    SELECT 1
                    FROM sys.index_columns ic
                    INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    WHERE ic.object_id = t.object_id AND ic.column_id = c.column_id AND ic.key_ordinal = 1
                );
                """;
            int indexedTenantTables = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            indexedTenantTables.Should().BeGreaterThanOrEqualTo(30);
        }

        // 4. Verify bounded widget query stays well within latency budget and handles unknown widget gracefully
        var sw = Stopwatch.StartNew();
        var widgetRes = await dashboard.QueryWidgetAsync(
            ownerAId, clinicA, "patients.summary",
            clock.UtcNow.AddDays(-7), clock.UtcNow.AddDays(1), null, deviceId);
        sw.Stop();
        widgetRes.IsSuccess.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000);

        var unknownWidget = await dashboard.QueryWidgetAsync(
            ownerAId, clinicA, "nonexistent.widget",
            clock.UtcNow.AddDays(-7), clock.UtcNow.AddDays(1), null, deviceId);
        unknownWidget.IsFailure.Should().BeTrue();
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة ١٦", "12 شارع النيل", "Cairo", "Cairo", "01000000016", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, slug, null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();
    }

    private static async Task<PlatformAdmin> EnsureEnrolledAdminAsync(
        CoreDbContext db,
        IPlatformAdminAuthService adminAuth,
        IHourlyTotpService totp,
        IntegrationTestClock clock)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        var firstLogin = await adminAuth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, null, null);
        firstLogin.IsSuccess.Should().BeTrue();
        string code = totp.ComputeCurrentCode(firstLogin.Value.ManualSetupKey!, clock.UtcNow);
        var enrollRes = await adminAuth.CompleteEnrollmentAsync(firstLogin.Value.AdminId, "SuperStrongPass#2026", code, null, null);
        enrollRes.IsSuccess.Should().BeTrue();
        return await db.PlatformAdmins.FirstAsync(a => a.Id == firstLogin.Value.AdminId);
    }

    private static async Task ActivateUserFromOutboxAsync(
        OutboxService outbox,
        FakeEmailSender emailSender,
        IAuthService authService,
        string email,
        string password)
    {
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        string code = Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
        var res = await authService.ActivateAccountAsync(email, code, password, "dev-activate", "TestBrowser", null, null);
        res.IsSuccess.Should().BeTrue();
    }
}


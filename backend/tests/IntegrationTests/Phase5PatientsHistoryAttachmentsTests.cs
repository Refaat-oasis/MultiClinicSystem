using System.Text.RegularExpressions;
using BuildingBlocks.Application;
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
public sealed class Phase5PatientsHistoryAttachmentsTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase5PatientsHistoryAttachmentsTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_ClinicWithPatientsModule_When_CreatingSearchingAndInspecting_Then_EnforcesNumberBlocksArabicSearchDuplicatesAndScoping()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var authzService = sp.GetRequiredService<IClinicAuthorizationService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p5-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P5", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");

        var clinicARes = await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة المرضى أ", null, null, null, "dev-owner", null, null);
        var clinicBRes = await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة المرضى ب", null, null, null, "dev-owner", null, null);
        Guid clinicAId = clinicARes.Value.ClinicId;
        Guid clinicBId = clinicBRes.Value.ClinicId;

        var enableModules = await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicAId,
            [
                new ModuleToggleInput("patients", true, "P5"),
                new ModuleToggleInput("patient-history", true, "P5"),
                new ModuleToggleInput("attachments", true, "P5")
            ],
            "SuperStrongPass#2026",
            null,
            null);
        enableModules.IsSuccess.Should().BeTrue();

        // Create two doctors in Clinic A so we can test Doctor & Specialty scoping
        string doc1Email = $"p5-doc1-{Guid.NewGuid():N}@clinic.local";
        string doc2Email = $"p5-doc2-{Guid.NewGuid():N}@clinic.local";
        var invDoc1 = await authzService.InviteStaffAsync(ownerRes.Value.UserId, clinicAId, "د. عادل", doc1Email, "Doctor", "dev-owner", null, null);
        var invDoc2 = await authzService.InviteStaffAsync(ownerRes.Value.UserId, clinicAId, "د. ليلى", doc2Email, "Doctor", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc1Email, "DocPass#2026");
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc2Email, "DocPass#2026");

        var doc1User = await db.Users.FirstAsync(u => u.Email == doc1Email);
        var doc2User = await db.Users.FirstAsync(u => u.Email == doc2Email);

        // Approve devices for doctors
        var d1 = await db.AppDevices.FirstAsync(d => d.UserId == doc1User.Id);
        await authzService.ApproveClinicDeviceAsync(ownerRes.Value.UserId, clinicAId, d1.DeviceId, "dev-owner", null, null);
        var d2 = await db.AppDevices.FirstAsync(d => d.UserId == doc2User.Id);
        await authzService.ApproveClinicDeviceAsync(ownerRes.Value.UserId, clinicAId, d2.DeviceId, "dev-owner", null, null);

        var docProfile1 = await structureService.CreateDoctorAsync(
            ownerRes.Value.UserId, clinicAId, doc1User.Id, "د. عادل", "استشاري", "Consultant", ["dental"], [], 20, "dev-owner", null, null);
        var docProfile2 = await structureService.CreateDoctorAsync(
            ownerRes.Value.UserId, clinicAId, doc2User.Id, "د. ليلى", "أخصائية", "Specialist", ["derma"], [], 20, "dev-owner", null, null);
        docProfile1.IsSuccess.Should().BeTrue();
        docProfile2.IsSuccess.Should().BeTrue();

        // Concurrent patient creations allocate unique P-000001..P-000006 without collisions
        async Task<BuildingBlocks.Domain.Result<PatientDetailResult>> CreatePatientConcurrentAsync(int idx)
        {
            var localCtx = new CurrentClinicContext();
            var (localProvider, _) = TestServiceFactory.CreateServices(localCtx, clock);
            using var s = localProvider.CreateScope();
            var svc = s.ServiceProvider.GetRequiredService<IPatientService>();
            return await svc.CreatePatientAsync(
                ownerRes.Value.UserId,
                clinicAId,
                new CreatePatientInput(
                    null,
                    idx == 0 ? "أحمد إبراهيم مصطفى" : $"مريض رقم {idx}",
                    $"0100123450{idx}",
                    null,
                    new DateOnly(1990, 1, 1),
                    null,
                    "Male",
                    null,
                    null,
                    idx % 2 == 0 ? docProfile1.Value.Id : docProfile2.Value.Id,
                    idx % 2 == 0 ? "dental" : "derma",
                    false,
                    null),
                "dev-owner",
                null,
                null);
        }

        var parallelResults = await Task.WhenAll(Enumerable.Range(0, 6).Select(CreatePatientConcurrentAsync));
        parallelResults.Should().OnlyContain(r => r.IsSuccess);
        var fileNumbers = parallelResults.Select(r => r.Value.FileNumber).Distinct().ToList();
        fileNumbers.Should().HaveCount(6);
        fileNumbers.Should().OnlyContain(f => f.StartsWith("P-"));

        PatientDetailResult primaryPatient = parallelResults[0].Value;
        PatientDetailResult secondaryPatient = parallelResults[1].Value;

        // OperationId idempotency returns the existing patient without allocating a new file number
        Guid opId = Guid.NewGuid();
        var idemFirst = await patientService.CreatePatientAsync(
            ownerRes.Value.UserId,
            clinicAId,
            new CreatePatientInput(null, "منى عبدالرحمن", "01223344556", null, null, 28, "Female", null, null, docProfile1.Value.Id, "dental", false, opId),
            "dev-owner",
            null,
            null);
        idemFirst.IsSuccess.Should().BeTrue();

        var idemReplay = await patientService.CreatePatientAsync(
            ownerRes.Value.UserId,
            clinicAId,
            new CreatePatientInput(null, "منى عبدالرحمن", "01223344556", null, null, 28, "Female", null, null, docProfile1.Value.Id, "dental", false, opId),
            "dev-owner",
            null,
            null);
        idemReplay.IsSuccess.Should().BeTrue();
        idemReplay.Value.Id.Should().Be(idemFirst.Value.Id);
        idemReplay.Value.FileNumber.Should().Be(idemFirst.Value.FileNumber);

        // Arabic search handles Alef / Ta marbuta / Ya maqsura variants
        var arabicSearch = await patientService.SearchPatientsAsync(ownerRes.Value.UserId, clinicAId, "احمد ابراهيم مصطفي", 10, "dev-owner");
        arabicSearch.IsSuccess.Should().BeTrue();
        arabicSearch.Value.Items.Should().Contain(p => p.Id == primaryPatient.Id);

        var taSearch = await patientService.SearchPatientsAsync(ownerRes.Value.UserId, clinicAId, "مني عبدالرحمن", 10, "dev-owner");
        taSearch.IsSuccess.Should().BeTrue();
        taSearch.Value.Items.Should().Contain(p => p.Id == idemFirst.Value.Id);

        // Duplicate warning stops unconfirmed create, succeeds when ConfirmDuplicateCreation = true
        var dupBlocked = await patientService.CreatePatientAsync(
            ownerRes.Value.UserId,
            clinicAId,
            new CreatePatientInput(null, "احمد ابراهيم مصطفي", "+20 100 123 4500", null, null, 35, "Male", null, null, null, null, false, null),
            "dev-owner",
            null,
            null);
        dupBlocked.IsFailure.Should().BeTrue();
        dupBlocked.Error.Code.Should().Be("DUPLICATE_PATIENT_WARNING");

        var dupConfirmed = await patientService.CreatePatientAsync(
            ownerRes.Value.UserId,
            clinicAId,
            new CreatePatientInput(null, "احمد ابراهيم مصطفي", "+20 100 123 4500", null, null, 35, "Male", null, null, null, null, true, null),
            "dev-owner",
            null,
            null);
        dupConfirmed.IsSuccess.Should().BeTrue();
        dupConfirmed.Value.DuplicateWarnings.Should().NotBeEmpty();

        // Add allergy, medical history, public + doctor-private note, and PNG attachment
        var allergyRes = await patientService.AddAllergyAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "Penicillin", "Severe", "Anaphylaxis", "dev-owner", null, null);
        allergyRes.IsSuccess.Should().BeTrue();

        var histRes = await patientService.UpdateMedicalHistoryAsync(
            ownerRes.Value.UserId,
            clinicAId,
            primaryPatient.Id,
            new UpdatePatientMedicalHistoryInput("Hypertension controlled", ["Hypertension"], ["Amlodipine 5mg"], [], true),
            "dev-owner",
            null,
            null);
        histRes.IsSuccess.Should().BeTrue();
        histRes.Value.IsMedicalHistoryReviewed.Should().BeTrue();

        // Doctor 1 adds a private note -> visible to Doc 1, hidden from Owner/other staff & Super Admin
        var privateNote = await patientService.AddNoteAsync(doc1User.Id, clinicAId, primaryPatient.Id, "Private psychiatric observation", true, "dev-activate", null, null);
        privateNote.IsSuccess.Should().BeTrue();

        var doc1Detail = await patientService.GetPatientAsync(doc1User.Id, clinicAId, primaryPatient.Id, "dev-activate");
        doc1Detail.Value.Notes.Should().Contain(n => n.Id == privateNote.Value.Id);

        var ownerDetail = await patientService.GetPatientAsync(ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "dev-owner");
        ownerDetail.Value.Notes.Should().NotContain(n => n.Id == privateNote.Value.Id);

        // Attachment validation (reject bad magic bytes, accept PNG, download original & thumb)
        var badUpload = await patientService.UploadAttachmentAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "Scan", "fake.png", "image/png", [1, 2, 3, 4, 5, 6, 7, 8], null, "dev-owner", null, null);
        badUpload.IsFailure.Should().BeTrue();
        badUpload.Error.Code.Should().Be("INVALID_FILE_SIGNATURE");

        byte[] cleanPng = BuildCleanPng();
        var uploadRes = await patientService.UploadAttachmentAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "XRay", "pano.png", "image/png", cleanPng, "Lower jaw", "dev-owner", null, null);
        uploadRes.IsSuccess.Should().BeTrue();
        uploadRes.Value.HasThumbnail.Should().BeTrue();

        var downloadRes = await patientService.DownloadAttachmentAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, uploadRes.Value.Id, thumbnail: false, "dev-owner");
        downloadRes.IsSuccess.Should().BeTrue();
        downloadRes.Value.Content.Should().Equal(cleanPng);

        // Doctor & Specialty Scoping: enable scoping on Clinic A -> Doc 1 sees primaryPatient (doc1/dental), gets 404 on secondaryPatient (doc2/derma)
        var scopeCfg = await patientService.ConfigurePatientScopingAsync(ownerRes.Value.UserId, clinicAId, true, true, "dev-owner", null, null);
        scopeCfg.IsSuccess.Should().BeTrue();

        var doc1List = await patientService.SearchPatientsAsync(doc1User.Id, clinicAId, null, 25, "dev-activate");
        doc1List.Value.Items.Should().Contain(p => p.Id == primaryPatient.Id);
        doc1List.Value.Items.Should().NotContain(p => p.Id == secondaryPatient.Id);

        var crossDocAccess = await patientService.GetPatientAsync(doc1User.Id, clinicAId, secondaryPatient.Id, "dev-activate");
        crossDocAccess.IsFailure.Should().BeTrue();
        crossDocAccess.Error.Code.Should().Be("PATIENT_NOT_FOUND");

        // Cross-clinic isolation: Clinic B cannot see Clinic A patient (404)
        var enableB = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicBId, [new ModuleToggleInput("patients", true, "P5")], "SuperStrongPass#2026", null, null);
        enableB.IsSuccess.Should().BeTrue();

        var crossClinicGet = await patientService.GetPatientAsync(ownerRes.Value.UserId, clinicBId, primaryPatient.Id, "dev-owner");
        crossClinicGet.IsFailure.Should().BeTrue();
        crossClinicGet.Error.Code.Should().Be("PATIENT_NOT_FOUND");

        // Module-off degradation: turn attachments off -> upload fails 403 MODULE_DISABLED, patient profile GET still succeeds with Attachments hidden
        var disableAttachments = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicAId, [new ModuleToggleInput("attachments", false, "Off test")], "SuperStrongPass#2026", null, null);
        disableAttachments.IsSuccess.Should().BeTrue();

        var attachOffUpload = await patientService.UploadAttachmentAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "Scan", "pano2.png", "image/png", cleanPng, null, "dev-owner", null, null);
        attachOffUpload.IsFailure.Should().BeTrue();
        attachOffUpload.Error.Code.Should().Be("MODULE_DISABLED");

        var patientWithAttachOff = await patientService.GetPatientAsync(ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "dev-owner");
        patientWithAttachOff.IsSuccess.Should().BeTrue();
        patientWithAttachOff.Value.IsAttachmentsEnabled.Should().BeFalse();
        patientWithAttachOff.Value.Attachments.Should().BeEmpty();
        patientWithAttachOff.Value.IsMedicalHistoryEnabled.Should().BeTrue();

        // Turn patient-history off -> allergy add fails 403 MODULE_DISABLED, patient GET still succeeds cleanly
        var disableHistory = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicAId, [new ModuleToggleInput("patient-history", false, "Off test")], "SuperStrongPass#2026", null, null);
        disableHistory.IsSuccess.Should().BeTrue();

        var historyOffAllergy = await patientService.AddAllergyAsync(
            ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "Sulfa", "Moderate", null, "dev-owner", null, null);
        historyOffAllergy.IsFailure.Should().BeTrue();
        historyOffAllergy.Error.Code.Should().Be("MODULE_DISABLED");

        var patientWithHistoryOff = await patientService.GetPatientAsync(ownerRes.Value.UserId, clinicAId, primaryPatient.Id, "dev-owner");
        patientWithHistoryOff.IsSuccess.Should().BeTrue();
        patientWithHistoryOff.Value.IsMedicalHistoryEnabled.Should().BeFalse();
        patientWithHistoryOff.Value.Allergies.Should().BeEmpty();

        // Super Admin inspect logs PatientAccessLog row
        var inspectRes = await patientService.InspectPatientAsSuperAdminAsync(admin.Id.ToString(), clinicAId, primaryPatient.Id, "Support audit #101");
        inspectRes.IsSuccess.Should().BeTrue();
        inspectRes.Value.Id.Should().Be(primaryPatient.Id);
        inspectRes.Value.Notes.Should().NotContain(n => n.Id == privateNote.Value.Id);

        var accessLog = await db.PatientAccessLogs.FirstOrDefaultAsync(l => l.ClinicId == clinicAId && l.Reason == "Support audit #101");
        accessLog.Should().NotBeNull();
        accessLog!.ActorType.Should().Be("SuperAdmin");
    }

    private static byte[] BuildCleanPng()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
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

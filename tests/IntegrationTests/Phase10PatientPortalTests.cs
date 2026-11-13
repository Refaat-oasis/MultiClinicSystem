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
public sealed class Phase10PatientPortalTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase10PatientPortalTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_PatientPortal_When_TestingInvitesPinActivationRecordVisibilitySignedUrlsAndModuleOff_Then_AllRulesHold()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero)
        };
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var planService = sp.GetRequiredService<ITreatmentPlanService>();
        var portalService = sp.GetRequiredService<IPatientPortalService>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p10-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P10", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة بسمة", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicAId = clinicARes.Value.ClinicId;
        string oldSlug = $"smile-old-{clinicAId:N}"[..16];
        string activeSlug = $"smile-dental-{clinicAId:N}"[..18];
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicAId, oldSlug, ["dental"]);
        await clinicsService.UpdateClinicSettingsAsync(
            ownerId, clinicAId,
            "عيادة بسمة", "10 شارع النيل", "Maadi", "Cairo", "01001112233", null, null, activeSlug, "UTC", "EGP", "None", null,
            null, null);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة النور ب", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicBId = clinicBRes.Value.ClinicId;
        string slugB = $"noor-b-{clinicBId:N}"[..14];
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicBId, slugB, ["dental"]);

        string[] modulesA =
        [
            "patients", "patient-history", "attachments", "scheduling", "clinical", "billing", "treatment-plans", "portal"
        ];
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicAId,
            modulesA.Select(m => new ModuleToggleInput(m, true, "P10")).ToList(),
            "SuperStrongPass#2026", null, null);

        // Clinic B: portal OFF
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicBId,
            [new ModuleToggleInput("patients", true, "P10")],
            "SuperStrongPass#2026", null, null);

        // 1. Slug landing resolution & historical redirect & portal-off 404 parity
        var redirectRes = await portalService.ResolvePublicLandingAsync(oldSlug);
        redirectRes.IsSuccess.Should().BeTrue();
        redirectRes.Value.IsRedirect.Should().BeTrue();
        redirectRes.Value.CurrentSlug.Should().Be(activeSlug);
        redirectRes.Value.RedirectUrl.Should().Be($"/c/{activeSlug}");

        var portalOffLanding = await portalService.ResolvePublicLandingAsync(slugB);
        portalOffLanding.IsFailure.Should().BeTrue();
        portalOffLanding.Error.Code.Should().Be("CLINIC_NOT_FOUND");

        var unknownLanding = await portalService.ResolvePublicLandingAsync("unknown-slug-404");
        unknownLanding.Error.Code.Should().Be("CLINIC_NOT_FOUND");

        // Setup patients in A (Adult + Minor + 2nd patient)
        var adultPat = await patientService.CreatePatientAsync(
            ownerId, clinicAId,
            new CreatePatientInput(null, "خالد محمود", "01010000101", null, new DateOnly(1992, 3, 15), null, "Male", null, null, null, "dental", false, null),
            "dev-owner", null, null);
        Guid adultId = adultPat.Value.Id;
        (await patientService.UpdatePatientPortalProfileAsync(ownerId, clinicAId, adultId, "khaled.p10@clinic.local", "O+", null, null, "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        var minorPat = await patientService.CreatePatientAsync(
            ownerId, clinicAId,
            new CreatePatientInput(null, "زين خالد", "01010000102", null, new DateOnly(2016, 5, 20), null, "Male", null, null, null, "dental", false, null),
            "dev-owner", null, null);
        Guid minorId = minorPat.Value.Id;

        var otherPat = await patientService.CreatePatientAsync(
            ownerId, clinicAId,
            new CreatePatientInput(null, "سارة عادل", "01010000103", null, new DateOnly(1995, 7, 10), null, "Female", null, null, null, "dental", false, null),
            "dev-owner", null, null);
        Guid otherPatId = otherPat.Value.Id;

        // 2. Invitation validation: consent required, email or printed pin required
        var noConsent = await portalService.InvitePatientAsync(
            ownerId, clinicAId, adultId,
            new InvitePatientToPortalInput("khaled.p10@clinic.local", false, null, false, "v1", false),
            "dev-owner", null, null);
        noConsent.IsFailure.Should().BeTrue();
        noConsent.Error.Code.Should().Be("CONSENT_REQUIRED");

        var noEmailNoPin = await portalService.InvitePatientAsync(
            ownerId, clinicAId, minorId,
            new InvitePatientToPortalInput(null, false, null, true, "v1", false),
            "dev-owner", null, null);
        noEmailNoPin.IsFailure.Should().BeTrue();
        noEmailNoPin.Error.Code.Should().Be("EMAIL_REQUIRED");

        // 3. Email invite -> 5 failed attempts lockout -> resend after cooldown -> activate
        string patEmail = $"khaled-{Guid.NewGuid():N}@patient.local";
        var emailInvite = await portalService.InvitePatientAsync(
            ownerId, clinicAId, adultId,
            new InvitePatientToPortalInput(patEmail, false, null, true, "v1", false),
            "dev-owner", null, null);
        emailInvite.IsSuccess.Should().BeTrue();
        emailInvite.Value.Status.Should().Be(PatientAccountStatus.Invited);

        for (int i = 0; i < 5; i++)
        {
            var badAct = await portalService.ActivateWithEmailCodeAsync(
                activeSlug,
                new ActivatePatientPortalInput(patEmail, "000000", "PatPass#2026"),
                null, null);
            badAct.IsFailure.Should().BeTrue();
        }

        var lockedAct = await portalService.ActivateWithEmailCodeAsync(
            activeSlug,
            new ActivatePatientPortalInput(patEmail, "000000", "PatPass#2026"),
            null, null);
        lockedAct.Error.Code.Should().Be("MAX_ATTEMPTS_EXCEEDED");

        // Resend cooldown enforced, then advance clock 61s and resend
        var immediateResend = await portalService.ResendInvitationAsync(ownerId, clinicAId, adultId, "dev-owner", null, null);
        immediateResend.Error.Code.Should().Be("RESEND_COOLDOWN");

        clock.UtcNow = clock.UtcNow.AddSeconds(61);
        var resendOk = await portalService.ResendInvitationAsync(ownerId, clinicAId, adultId, "dev-owner", null, null);
        resendOk.IsSuccess.Should().BeTrue();

        await outbox.ProcessPendingMessagesAsync();
        string actCode = Regex.Match(emailSender.SentMessages.Last(m => m.To == patEmail).Body, @"\b(\d{6})\b").Groups[1].Value;
        var activated = await portalService.ActivateWithEmailCodeAsync(
            activeSlug,
            new ActivatePatientPortalInput(patEmail, actCode, "PatPass#2026"),
            null, null);
        activated.IsSuccess.Should().BeTrue();
        Guid adultAccountId = activated.Value.PatientAccountId;

        // Replay of single-use email code must fail
        var replayAct = await portalService.ActivateWithEmailCodeAsync(
            activeSlug,
            new ActivatePatientPortalInput(patEmail, actCode, "PatPass#2026"),
            null, null);
        replayAct.IsFailure.Should().BeTrue();

        // 4. Printed PIN flow for guardian minor without email
        var pinInvite = await portalService.InvitePatientAsync(
            ownerId, clinicAId, minorId,
            new InvitePatientToPortalInput(null, true, "والد زين", true, "v1", true),
            "dev-owner", null, null);
        pinInvite.IsSuccess.Should().BeTrue();
        string printedPin = pinInvite.Value.GeneratedPrintedPin!;
        printedPin.Should().HaveLength(10);

        // Wrong DOB fails
        var wrongDob = await portalService.RegisterPrintedPinAndSendEmailCodeAsync(
            activeSlug,
            new RegisterPrintedPinInput(minorPat.Value.FileNumber, new DateOnly(2000, 1, 1), printedPin, $"minor-{Guid.NewGuid():N}@patient.local"),
            null, null);
        wrongDob.IsFailure.Should().BeTrue();

        string guardianEmail = $"guardian-{Guid.NewGuid():N}@patient.local";
        var registerPinOk = await portalService.RegisterPrintedPinAndSendEmailCodeAsync(
            activeSlug,
            new RegisterPrintedPinInput(minorPat.Value.FileNumber, new DateOnly(2016, 5, 20), printedPin, guardianEmail),
            null, null);
        registerPinOk.IsSuccess.Should().BeTrue();

        // Single-use printed PIN replay fails
        var replayPin = await portalService.RegisterPrintedPinAndSendEmailCodeAsync(
            activeSlug,
            new RegisterPrintedPinInput(minorPat.Value.FileNumber, new DateOnly(2016, 5, 20), printedPin, $"other-{Guid.NewGuid():N}@patient.local"),
            null, null);
        replayPin.IsFailure.Should().BeTrue();

        // 5. Medical background, notes, attachments, visits, prescription PDF, upcoming appt, plan costs
        await patientService.AddAllergyAsync(ownerId, clinicAId, adultId, "Penicillin", "Severe", "Anaphylaxis", "dev-owner", null, null);
        await patientService.UpdateMedicalHistoryAsync(
            ownerId, clinicAId, adultId,
            new UpdatePatientMedicalHistoryInput("Controlled", ["Hypertension"], ["Amlodipine"], ["Appendectomy"], true),
            "dev-owner", null, null);

        var privNote = await patientService.AddNoteAsync(ownerId, clinicAId, adultId, "Doctor private note", true, "dev-owner", null, null);
        var shareNote = await patientService.AddNoteAsync(ownerId, clinicAId, adultId, "Rinse with warm saline", false, "dev-owner", null, null);
        // Attempt sharing private note is rejected
        (await patientService.SetNotePortalVisibilityAsync(ownerId, clinicAId, adultId, privNote.Value.Id, true, "dev-owner", null, null)).IsFailure.Should().BeTrue();
        (await patientService.SetNotePortalVisibilityAsync(ownerId, clinicAId, adultId, shareNote.Value.Id, true, "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        byte[] pdfMagic = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray();
        var unsharedAtt = await patientService.UploadAttachmentAsync(ownerId, clinicAId, adultId, "Lab", "internal.pdf", "application/pdf", pdfMagic, null, "dev-owner", null, null);
        var sharedAtt = await patientService.UploadAttachmentAsync(ownerId, clinicAId, adultId, "Report", "shared.pdf", "application/pdf", pdfMagic, "X-ray summary", "dev-owner", null, null);
        (await patientService.SetAttachmentPortalVisibilityAsync(ownerId, clinicAId, adultId, sharedAtt.Value.Id, true, "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        // Struct, appointment, visit, plan
        var roomA = await structureService.CreateRoomAsync(ownerId, clinicAId, "R10", "غرفة 10", "Room 10", "dental", null, "dev-owner", null, null);
        var docA = await structureService.CreateDoctorAsync(ownerId, clinicAId, ownerId, "د. ليلى", "Dr Laila", "Dentist", ["dental"], [roomA.Value.Id], 30, "dev-owner", null, null);
        var catA = await schedulingService.CreateCategoryAsync(ownerId, clinicAId, "dental", "متابعة", "Followup", 30, 300m, "EGP", true, "dev-owner", null, null);
        await structureService.SaveWorkingHoursAsync(ownerId, clinicAId, null, Enumerable.Range(0, 7).Select(d => new WorkingHourInput(d, "08:00", "18:00", false)).ToList(), "dev-owner", null, null);

        await schedulingService.BookAppointmentAsync(
            ownerId, clinicAId,
            new BookAppointmentInput(null, adultId, docA.Value.Id, roomA.Value.Id, catA.Value.Id, clock.UtcNow.AddDays(2), clock.UtcNow.AddDays(2).AddMinutes(30), "Normal", null, "Upcoming", null),
            "dev-owner", null, null);

        var visitRes = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicAId,
            new StartVisitInput(null, null, adultId, docA.Value.Id, "dental", roomA.Value.Id, 30, true, null),
            "dev-owner", null, null);
        Guid visitId = visitRes.Value.Id;
        await clinicalService.UpdateVisitNotesAsync(
            ownerId, clinicAId, visitId,
            new UpdateVisitNotesInput("Pain", null, null, "Molar caries", "Brush twice daily", "Secret doctor note", null, true),
            "dev-owner", null, null);
        var rxRes = await clinicalService.CreatePrescriptionAsync(
            ownerId, clinicAId, visitId,
            new CreatePrescriptionInput("After meals", false, [new PrescriptionItemInput("Paracetamol", "Acetaminophen", "500mg", "TID", "5d", null)], null),
            "dev-owner", null, null);
        await clinicalService.CompleteVisitAsync(ownerId, clinicAId, visitId, new CompleteVisitInput(false, null, null), "dev-owner", null, null);

        var otherVisit = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicAId,
            new StartVisitInput(null, null, otherPatId, docA.Value.Id, "dental", roomA.Value.Id, 30, true, null),
            "dev-owner", null, null);
        await clinicalService.CompleteVisitAsync(ownerId, clinicAId, otherVisit.Value.Id, new CompleteVisitInput(false, null, null), "dev-owner", null, null);

        var overview = await portalService.GetHomeOverviewAsync(adultAccountId, clinicAId, adultId, activeSlug);
        overview.IsSuccess.Should().BeTrue();
        overview.Value.MedicalBackground!.Allergies.Should().ContainSingle(a => a.Allergen == "Penicillin");
        overview.Value.MedicalBackground.VisibleNotes.Should().ContainSingle(n => n.NoteId == shareNote.Value.Id);
        overview.Value.MedicalBackground.VisibleNotes.Should().NotContain(n => n.NoteId == privNote.Value.Id);
        overview.Value.MedicalBackground.SharedAttachments.Should().ContainSingle(a => a.AttachmentId == sharedAtt.Value.Id);
        overview.Value.MedicalBackground.SharedAttachments.Should().NotContain(a => a.AttachmentId == unsharedAtt.Value.Id);
        overview.Value.UpcomingAppointments.Should().HaveCount(1);
        overview.Value.Visits.Should().ContainSingle(v => v.VisitId == visitId);

        // Cross-patient visit 404
        var crossPatVisit = await portalService.GetVisitDetailAsync(adultAccountId, clinicAId, adultId, activeSlug, otherVisit.Value.Id);
        crossPatVisit.IsFailure.Should().BeTrue();
        crossPatVisit.Error.Code.Should().Be("VISIT_NOT_FOUND");

        // Signed URL works now, expires after TTL
        var signedUrl = await portalService.CreateSignedPrescriptionUrlAsync(adultAccountId, clinicAId, adultId, activeSlug, rxRes.Value.Id);
        signedUrl.IsSuccess.Should().BeTrue();
        string rawTok = Uri.UnescapeDataString(signedUrl.Value.Url.Split("token=")[1]);

        var dlOk = await portalService.ExecuteSignedDownloadAsync(activeSlug, "prescription", rxRes.Value.Id, adultId, rawTok);
        dlOk.IsSuccess.Should().BeTrue();
        dlOk.Value.ContentType.Should().Be("application/pdf");

        var dlWrongPatient = await portalService.ExecuteSignedDownloadAsync(activeSlug, "prescription", rxRes.Value.Id, otherPatId, rawTok);
        dlWrongPatient.IsFailure.Should().BeTrue();

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        var dlExpired = await portalService.ExecuteSignedDownloadAsync(activeSlug, "prescription", rxRes.Value.Id, adultId, rawTok);
        dlExpired.IsFailure.Should().BeTrue();
        dlExpired.Error.Code.Should().Be("INVALID_OR_EXPIRED_DOWNLOAD_TOKEN");

        // 6. Hiding visit hides it immediately
        await clinicalService.SetVisitPortalVisibilityAsync(ownerId, clinicAId, visitId, false, "dev-owner", null, null);
        var visitsAfterHide = await portalService.GetVisitsSectionAsync(adultAccountId, clinicAId, adultId, activeSlug);
        visitsAfterHide.Value.Should().BeEmpty();

        // 7. Generic forgot password & reset revokes stamp
        ctx.SetTenant(clinicAId);
        string oldStamp = (await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.Id == adultAccountId)).SecurityStamp;
        ctx.Clear();
        var unknownForgot = await portalService.ForgotPasswordAsync(activeSlug, "nobody@nowhere.local", null, null);
        unknownForgot.IsSuccess.Should().BeTrue();

        var knownForgot = await portalService.ForgotPasswordAsync(activeSlug, patEmail, null, null);
        knownForgot.IsSuccess.Should().BeTrue();
        await outbox.ProcessPendingMessagesAsync();
        string resetCode = Regex.Match(emailSender.SentMessages.Last(m => m.To == patEmail).Body, @"\b(\d{6})\b").Groups[1].Value;

        var resetOk = await portalService.ResetPasswordAsync(
            activeSlug,
            new PatientPortalResetPasswordInput(patEmail, resetCode, "NewPatPass#2026"),
            null, null);
        resetOk.IsSuccess.Should().BeTrue();
        ctx.SetTenant(clinicAId);
        string newStamp = (await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.Id == adultAccountId)).SecurityStamp;
        ctx.Clear();
        newStamp.Should().NotBe(oldStamp);

        // 8. Access logs & portal dashboard widgets & Super Admin inspect
        var logs = await portalService.ListPatientPortalAccessLogsAsync(ownerId, clinicAId, adultId, 1, 50, "dev-owner");
        logs.IsSuccess.Should().BeTrue();
        logs.Value.Should().NotBeEmpty();

        var accountsWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "portal.accounts-summary", null, null, null, "dev-owner");
        accountsWidget.IsSuccess.Should().BeTrue();
        accountsWidget.Value.PrimaryValue.Should().Be(2m); // adult + minor

        var inspectPortal = await portalService.InspectClinicPortalAsync(admin.Id.ToString(), clinicAId);
        inspectPortal.IsSuccess.Should().BeTrue();
        inspectPortal.Value.TotalAccountsCount.Should().Be(2);
        inspectPortal.Value.ActiveAccountsCount.Should().Be(1);

        // ReadOnly clinic still permits portal reads; Suspended hides portal
        await adminService.UpdateClinicStatusAsync(admin.Id, clinicAId, "ReadOnly", null, null, null, null);
        var readOnlyOverview = await portalService.GetHomeOverviewAsync(adultAccountId, clinicAId, adultId, activeSlug);
        readOnlyOverview.IsSuccess.Should().BeTrue();

        await adminService.UpdateClinicStatusAsync(admin.Id, clinicAId, "Suspended", null, null, null, null);
        var suspendedLanding = await portalService.ResolvePublicLandingAsync(activeSlug);
        suspendedLanding.Error.Code.Should().Be("CLINIC_NOT_FOUND");
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة بوابة", "شارع النيل", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
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

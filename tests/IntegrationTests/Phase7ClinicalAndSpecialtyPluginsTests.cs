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
public sealed class Phase7ClinicalAndSpecialtyPluginsTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase7ClinicalAndSpecialtyPluginsTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_ClinicalAndSpecialtyPlugins_When_RunningDentalAndOphthalmologyFlows_Then_TimerAllergiesPdfOutboxChartFormsPortalAndIsolationAllHold()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 6, 8, 7, 0, 0, TimeSpan.Zero) // Monday
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
        var authzService = sp.GetRequiredService<IClinicAuthorizationService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var portalQueries = sp.GetRequiredService<IClinicalPortalQueries>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p7-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P7", ownerEmail, null, 4, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        // Clinic A: Dental Clinic
        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الأسنان السريرية", "UTC", "EGP", "NearestPound", "dev-owner", null, null);
        Guid clinicAId = clinicARes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicAId, ["dental"]);

        // Clinic B: Ophthalmology Clinic
        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الرمد السريرية", "UTC", "EGP", "NearestPound", "dev-owner", null, null);
        Guid clinicBId = clinicBRes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicBId, ["ophthalmology"]);

        string[] requiredModules = ["patients", "patient-history", "scheduling", "queue", "clinical"];
        foreach (Guid cid in new[] { clinicAId, clinicBId })
        {
            var modRes = await adminService.UpdateClinicModulesAsync(
                admin.Id,
                cid,
                requiredModules.Select(m => new ModuleToggleInput(m, true, "P7")).ToList(),
                "SuperStrongPass#2026",
                null,
                null);
            modRes.IsSuccess.Should().BeTrue();
        }

        // Clinic A setup: Monday 09:00-14:00, room, 2 doctors, category (20 min)
        await structureService.SaveWorkingHoursAsync(ownerId, clinicAId, null, [new WorkingHourInput((int)DayOfWeek.Monday, "09:00", "14:00", false)], "dev-owner", null, null);
        var roomA = await structureService.CreateRoomAsync(ownerId, clinicAId, "D1", "غرفة أسنان 1", "Dental Room 1", "dental", null, "dev-owner", null, null);

        string doc1Email = $"p7-doc1-{Guid.NewGuid():N}@clinic.local";
        var doc1User = await authzService.InviteStaffAsync(ownerId, clinicAId, "Dr Dental 1", doc1Email, "Doctor", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc1Email, "DocPass#2026");

        string doc2Email = $"p7-doc2-{Guid.NewGuid():N}@clinic.local";
        var doc2User = await authzService.InviteStaffAsync(ownerId, clinicAId, "Dr Dental 2", doc2Email, "Doctor", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc2Email, "DocPass#2026");

        var doc1 = await structureService.CreateDoctorAsync(ownerId, clinicAId, doc1User.Value.UserId, "د. أسنان 1", "Dr Dental 1", "Specialist", ["dental"], [roomA.Value.Id], 20, "dev-owner", null, null);
        var doc2 = await structureService.CreateDoctorAsync(ownerId, clinicAId, doc2User.Value.UserId, "د. أسنان 2", "Dr Dental 2", "Specialist", ["dental"], [roomA.Value.Id], 20, "dev-owner", null, null);

        await ApproveAllPendingDevicesAsync(authzService, ownerId, clinicAId);

        var catA = await schedulingService.CreateCategoryAsync(ownerId, clinicAId, "dental", "حشو وعلاج", "Dental Session", 20, 300m, "EGP", true, "dev-owner", null, null);
        var patientA = await patientService.CreatePatientAsync(ownerId, clinicAId, new CreatePatientInput(null, "فاطمة علي", "01020000001", null, null, 28, "Female", null, null, null, null, false, null), "dev-owner", null, null);
        await patientService.AddAllergyAsync(ownerId, clinicAId, patientA.Value.Id, "Penicillin", "Severe", "Anaphylaxis", "dev-owner", null, null);

        // Book 09:00-09:20 appointment -> Arrive -> Start visit
        DateTimeOffset nineAm = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);
        clock.UtcNow = nineAm.AddMinutes(-30);
        var appt = await schedulingService.BookAppointmentAsync(
            ownerId, clinicAId,
            new BookAppointmentInput(null, patientA.Value.Id, doc1.Value.Id, roomA.Value.Id, catA.Value.Id, nineAm, null, "Normal", null, null, null),
            "dev-owner", null, null);
        appt.IsSuccess.Should().BeTrue();
        (await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicAId, appt.Value.Id, "Arrived", null, "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        clock.UtcNow = nineAm;
        Guid opStartId = GuidV7.NewGuid();
        var visit1 = await clinicalService.StartOrResumeVisitAsync(
            doc1User.Value.UserId, clinicAId,
            new StartVisitInput(null, appt.Value.Id, null, null, null, null, null, true, opStartId),
            "dev-activate", null, null);
        visit1.IsSuccess.Should().BeTrue();
        visit1.Value.SupportsDentalChart.Should().BeTrue();
        visit1.Value.PatientAllergies.Should().ContainSingle(a => a.Allergen == "Penicillin");
        visit1.Value.ElapsedMinutes.Should().Be(0);
        visit1.Value.OvertimeMinutes.Should().Be(0);

        // Simulate 25 minutes passing & client reloading/resuming the same appointment session
        clock.UtcNow = nineAm.AddMinutes(25);
        var resumed = await clinicalService.StartOrResumeVisitAsync(
            doc1User.Value.UserId, clinicAId,
            new StartVisitInput(null, appt.Value.Id, null, null, null, null, null, true, null),
            "dev-activate", null, null);
        resumed.IsSuccess.Should().BeTrue();
        resumed.Value.Id.Should().Be(visit1.Value.Id);
        resumed.Value.ElapsedMinutes.Should().Be(25);
        resumed.Value.OvertimeMinutes.Should().Be(5);

        // Cross-doctor scoping: Dr Dental 2 cannot see Dr Dental 1's visit -> 404 VISIT_NOT_FOUND
        var crossDoctor = await clinicalService.GetVisitAsync(doc2User.Value.UserId, clinicAId, visit1.Value.Id, "dev-activate");
        crossDoctor.IsFailure.Should().BeTrue();
        crossDoctor.Error.Code.Should().Be("VISIT_NOT_FOUND");

        // Record vitals + verify BMI calculation (80 / 1.6^2 = 31.25)
        var vitals = await clinicalService.RecordVitalsAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new RecordVisitVitalsInput(120, 80, 74, 36.8m, 80m, 160m, 99, 95m),
            "dev-activate", null, null);
        vitals.IsSuccess.Should().BeTrue();
        vitals.Value.Bmi.Should().Be(31.25m);

        // Update clinical notes with PrivateDoctorNotes
        await clinicalService.UpdateVisitNotesAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new UpdateVisitNotesInput("ألم بالضرس", null, "Caries on 16", "Deep caries #16", "Avoid cold drinks", "CONFIDENTIAL: anxious patient", new DateOnly(2026, 6, 15), true),
            "dev-activate", null, null);

        // ICD-10 diagnosis + procedure + lab order + result
        var diag = await clinicalService.AddDiagnosisAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new AddVisitDiagnosisInput("K02.9", "تسوس الأسنان غير محدد", "Dental caries, unspecified", true, "Tooth 16"),
            "dev-activate", null, null);
        diag.IsSuccess.Should().BeTrue();

        var proc = await clinicalService.AddProcedureAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new AddVisitProcedureInput("D2392", "حشو تجميلي", "Composite filling", "16", null),
            "dev-activate", null, null);
        proc.IsSuccess.Should().BeTrue();

        var labOrder = await clinicalService.CreateLabOrderAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new CreateLabOrderInput("Apex Dental Lab", "Zirconia Crown", "16", "Shade A2"),
            "dev-activate", null, null);
        labOrder.IsSuccess.Should().BeTrue();
        var labDone = await clinicalService.RecordLabResultAsync(doc1User.Value.UserId, clinicAId, labOrder.Value.Id, "Delivered Shade A2", "dev-activate", null, null);
        labDone.Value.Status.Should().Be("ResultRecorded");

        // Tooth chart: record FDI 16 Caries, Primary FDI 55 Filled, Universal 14 -> Crown
        (await clinicalService.RecordToothStateAsync(doc1User.Value.UserId, clinicAId, visit1.Value.Id, new RecordToothStateInput("16", "FDI", "Caries", ["O", "M"], "Initial"), "dev-activate", null, null)).IsSuccess.Should().BeTrue();
        (await clinicalService.RecordToothStateAsync(doc1User.Value.UserId, clinicAId, visit1.Value.Id, new RecordToothStateInput("55", "FDI", "Filled", ["O"], "Primary molar"), "dev-activate", null, null)).IsSuccess.Should().BeTrue();
        (await clinicalService.RecordToothStateAsync(doc1User.Value.UserId, clinicAId, visit1.Value.Id, new RecordToothStateInput("14", "Universal", "Crown", null, "Upper left first molar"), "dev-activate", null, null)).IsSuccess.Should().BeTrue();

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        (await clinicalService.RecordToothStateAsync(doc1User.Value.UserId, clinicAId, visit1.Value.Id, new RecordToothStateInput("16", "FDI", "Filled", ["O", "M"], "Restored"), "dev-activate", null, null)).IsSuccess.Should().BeTrue();

        var chart = await clinicalService.GetPatientDentalChartAsync(doc1User.Value.UserId, clinicAId, patientA.Value.Id, "dev-activate");
        chart.IsSuccess.Should().BeTrue();
        chart.Value.Should().Contain(t => t.FdiToothNumber == "16" && t.UniversalToothCode == "3" && t.Condition == "Filled");
        chart.Value.Should().Contain(t => t.FdiToothNumber == "26" && t.UniversalToothCode == "14" && t.Condition == "Crown");
        chart.Value.Should().Contain(t => t.FdiToothNumber == "55" && t.IsPrimaryTooth && t.Condition == "Filled");
        ctx.SetTenant(clinicAId);
        int rawToothHistoryCount = await db.ToothRecords.CountAsync(t => t.ClinicId == clinicAId && t.PatientId == patientA.Value.Id && t.FdiToothNumber == "16");
        ctx.Clear();
        rawToothHistoryCount.Should().Be(2);

        // Allergy conflict detection & acknowledged prescription + printable PDF
        var rxBlocked = await clinicalService.CreatePrescriptionAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new CreatePrescriptionInput("Take after food", false, [new PrescriptionItemInput("Amoxicillin 500mg", "Penicillin", "500mg", "TID", "7 days", null)], null),
            "dev-activate", null, null);
        rxBlocked.IsFailure.Should().BeTrue();
        rxBlocked.Error.Code.Should().Be("ALLERGY_WARNING_CONFIRMATION_REQUIRED");

        var rxAllowed = await clinicalService.CreatePrescriptionAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new CreatePrescriptionInput("Patient confirmed desensitized", true, [new PrescriptionItemInput("Amoxicillin 500mg", "Penicillin", "500mg", "TID", "7 days", null)], null),
            "dev-activate", null, null);
        rxAllowed.IsSuccess.Should().BeTrue();
        rxAllowed.Value.HasAllergyWarning.Should().BeTrue();

        var pdf = await clinicalService.GetPrescriptionPdfAsync(doc1User.Value.UserId, clinicAId, rxAllowed.Value.Id, "dev-activate");
        pdf.IsSuccess.Should().BeTrue();
        string pdfText = Encoding.ASCII.GetString(pdf.Value.ContentBytes);
        pdfText.Should().StartWith("%PDF-1.4");
        pdfText.Should().Contain("Amoxicillin 500mg");
        pdfText.Should().Contain(patientA.Value.FileNumber);

        // Complete visit twice -> SessionCompleted appears EXACTLY ONCE in outbox
        var complete1 = await clinicalService.CompleteVisitAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new CompleteVisitInput(true, "Complex restoration", null),
            "dev-activate", null, null);
        complete1.IsSuccess.Should().BeTrue();
        complete1.Value.Status.Should().Be("Completed");
        complete1.Value.IsOvertimeExcused.Should().BeTrue();

        var complete2 = await clinicalService.CompleteVisitAsync(
            doc1User.Value.UserId, clinicAId, visit1.Value.Id,
            new CompleteVisitInput(true, "Complex restoration", null),
            "dev-activate", null, null);
        complete2.IsSuccess.Should().BeTrue();

        string visitToken = visit1.Value.Id.ToString();
        int outboxCompletedCount = await db.OutboxMessages
            .CountAsync(m => m.Type == "SessionCompleted" && m.Content.Contains(visitToken));
        outboxCompletedCount.Should().Be(1);

        // Portal isolation check: create a hidden walk-in visit -> Portal returns ONLY visit1 and NEVER PrivateDoctorNotes + logs PatientAccessLog
        var hiddenVisit = await clinicalService.StartOrResumeVisitAsync(
            doc1User.Value.UserId, clinicAId,
            new StartVisitInput(null, null, patientA.Value.Id, doc1.Value.Id, "dental", roomA.Value.Id, 15, false, null),
            "dev-activate", null, null);
        hiddenVisit.IsSuccess.Should().BeTrue();
        await clinicalService.CompleteVisitAsync(doc1User.Value.UserId, clinicAId, hiddenVisit.Value.Id, new CompleteVisitInput(false, null, null), "dev-activate", null, null);

        var portalVisits = await portalQueries.ListPatientVisibleVisitsAsync(clinicAId, patientA.Value.Id);
        portalVisits.IsSuccess.Should().BeTrue();
        portalVisits.Value.Should().ContainSingle(v => v.VisitId == visit1.Value.Id);
        portalVisits.Value.Should().NotContain(v => v.VisitId == hiddenVisit.Value.Id);
        var patientLog = await db.PatientAccessLogs.AnyAsync(l => l.ClinicId == clinicAId && l.ActorType == "Patient" && l.Reason == "PortalListVisits");
        patientLog.Should().BeTrue();

        // Clinical Dashboard widgets
        var summaryWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "clinical.sessions-summary", nineAm.AddDays(-1), nineAm.AddDays(1), null, "dev-owner");
        summaryWidget.IsSuccess.Should().BeTrue();
        summaryWidget.Value.PrimaryValue.Should().BeGreaterThanOrEqualTo(2m);
        var overtimeWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "clinical.overtime-sessions", nineAm.AddDays(-1), nineAm.AddDays(1), null, "dev-owner");
        overtimeWidget.IsSuccess.Should().BeTrue();
        overtimeWidget.Value.PrimaryValue.Should().BeGreaterThanOrEqualTo(1m);

        // Clinic B: Ophthalmology JSON-Schema form validation & zero dental records
        var roomB = await structureService.CreateRoomAsync(ownerId, clinicBId, "OPT1", "غرفة رمد", "Eye Room", "ophthalmology", null, "dev-owner", null, null);
        var docB = await structureService.CreateDoctorAsync(ownerId, clinicBId, ownerId, "د. رمد", "Dr Eye", "Consultant", ["ophthalmology"], [roomB.Value.Id], 20, "dev-owner", null, null);
        var patientB = await patientService.CreatePatientAsync(ownerId, clinicBId, new CreatePatientInput(null, "سامي حسن", "01020000002", null, null, 45, "Male", null, null, null, null, false, null), "dev-owner", null, null);

        string schema = """{"type":"object","required":["vaRight","iopMmHg"],"properties":{"vaRight":{"type":"string"},"iopMmHg":{"type":"number","minimum":5,"maximum":60}}}""";
        var formRes = await clinicalService.CreateSpecialtyFormAsync(ownerId, clinicBId, "ophthalmology", "فحص قياس النظر", "Eye Refraction Form", schema, "dev-owner", null, null);
        formRes.IsSuccess.Should().BeTrue();

        var visitB = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicBId,
            new StartVisitInput(null, null, patientB.Value.Id, docB.Value.Id, "ophthalmology", roomB.Value.Id, 20, true, null),
            "dev-owner", null, null);
        visitB.IsSuccess.Should().BeTrue();
        visitB.Value.SupportsDentalChart.Should().BeFalse();

        var invalidForm = await clinicalService.SaveVisitFormDataAsync(
            ownerId, clinicBId, visitB.Value.Id,
            new SaveVisitFormDataInput(formRes.Value.Id, """{"vaRight":"6/6","iopMmHg":95}"""),
            "dev-owner", null, null);
        invalidForm.IsFailure.Should().BeTrue();
        invalidForm.Error.Code.Should().Be("FORM_VALIDATION_FAILED");

        var validForm = await clinicalService.SaveVisitFormDataAsync(
            ownerId, clinicBId, visitB.Value.Id,
            new SaveVisitFormDataInput(formRes.Value.Id, """{"vaRight":"6/6","iopMmHg":16}"""),
            "dev-owner", null, null);
        validForm.IsSuccess.Should().BeTrue();

        ctx.SetTenant(clinicBId);
        int clinicBTeeth = await db.ToothRecords.CountAsync(t => t.ClinicId == clinicBId);
        ctx.Clear();
        clinicBTeeth.Should().Be(0);

        // Module-off isolation: turn clinical OFF in Clinic A -> clinical fails MODULE_DISABLED, scheduling continues working
        await adminService.UpdateClinicModulesAsync(admin.Id, clinicAId, [new ModuleToggleInput("clinical", false, "Off")], "SuperStrongPass#2026", null, null);
        var clinicalOff = await clinicalService.GetVisitAsync(ownerId, clinicAId, visit1.Value.Id, "dev-owner");
        clinicalOff.IsFailure.Should().BeTrue();
        clinicalOff.Error.Code.Should().Be("MODULE_DISABLED");

        var schedStillWorks = await schedulingService.ListAppointmentsAsync(ownerId, clinicAId, null, null, null, null, "dev-owner");
        schedStillWorks.IsSuccess.Should().BeTrue();
    }

    private static async Task OnboardClinicAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة اختبار", "شارع 10", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, $"p7-{clinicId:N}"[..18], null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();
    }

    private static async Task ApproveAllPendingDevicesAsync(IClinicAuthorizationService authzService, Guid ownerId, Guid clinicId)
    {
        var devices = await authzService.ListClinicDevicesAsync(ownerId, clinicId, "dev-owner");
        foreach (var d in devices.Value.Where(x => !x.IsApproved))
        {
            await authzService.ApproveClinicDeviceAsync(ownerId, clinicId, d.DeviceId, "dev-owner", null, null);
        }
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

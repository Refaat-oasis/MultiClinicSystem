using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase7Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate7_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase7Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 8, 55, 0, TimeSpan.Zero)); // Monday 08:55Z (within 15-min JWT TTL)
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }

                services.AddDbContext<CoreDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
                    options.UseSqlServer(GateConnectionString)
                           .AddInterceptors(interceptor);
                });

                var clockDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IClock));
                if (clockDescriptor != null)
                {
                    services.Remove(clockDescriptor);
                }

                services.AddSingleton<IClock>(_clock);
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.ApplyRowLevelSecurityAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Phase7_IntegrationGate_ClinicalSessionSpecialtyPluginsAndPortalIsolation()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        string adminToken = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g7-owner-a-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g7-owner-b-{Guid.NewGuid():N}@clinic.local";

        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G7 A",
            Email = ownerAEmail,
            MaxClinics = 2
        });
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G7 B",
            Email = ownerBEmail,
            MaxClinics = 2
        });

        using var clinicAClient = _factory.CreateClient();
        clinicAClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerAToken = await ActivateAndLoginClinicUserAsync(clinicAClient, ownerAEmail, "OwnerPass#2026", "dev-owner-a");
        clinicAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAToken);

        using var clinicBClient = _factory.CreateClient();
        clinicBClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerBToken = await ActivateAndLoginClinicUserAsync(clinicBClient, ownerBEmail, "OwnerPass#2026", "dev-owner-b");
        clinicBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBToken);

        var createClinicA = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 7 أسنان", "UTC", "EGP", "NearestPound", "dev-owner-a"));
        var createClinicB = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 7 رمد", "UTC", "EGP", "NearestPound", "dev-owner-b"));
        Guid clinicAId = (await createClinicA.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await createClinicB.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        foreach (Guid cid in new[] { clinicAId, clinicBId })
        {
            var modResp = await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{cid}/modules", new UpdateClinicModulesDto
            {
                StepUpPassword = "SuperAdminGate#2026",
                Modules =
                [
                    new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "patient-history", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "queue", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true }
                ]
            });
            modResp.EnsureSuccessStatusCode();
        }

        await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/working-hours", new SaveWorkingHoursDto
        {
            DoctorId = null,
            Items = [new WorkingHourItemDto { DayOfWeek = (int)DayOfWeek.Monday, StartTime = "09:00", EndTime = "13:00", IsClosed = false }]
        });

        var docAResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. أسنان",
            TitleAr = "طبيب أسنان",
            TitleEn = "Dentist",
            SpecialtyCodes = ["dental"],
            AssignedRoomIds = [],
            SlotDurationMinutes = 20
        });
        docAResp.EnsureSuccessStatusCode();
        var docA = await docAResp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var catAResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/categories", new UpsertAppointmentCategoryDto
        {
            SpecialtyCode = "dental",
            NameAr = "فحص وعلاج",
            NameEn = "Dental Visit",
            DefaultDurationMinutes = 10,
            DefaultPriceAmount = 300m,
            BookableOnline = true
        });
        catAResp.EnsureSuccessStatusCode();
        var catA = await catAResp.Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();

        var patAResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(null, "مريضة 7 أ", "01070000001", null, null, 31, "Female", null, null, null, null, false, null));
        patAResp.EnsureSuccessStatusCode();
        var patA = await patAResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        var allergyResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patA!.Id}/allergies",
            new AddPatientAllergyDto("Penicillin", "Severe", "Rash"));
        allergyResp.EnsureSuccessStatusCode();

        // Book 09:00-09:10 at 08:55 -> Arrived -> Start Visit at 08:56 -> Advance to 09:09 (+13m, +3m overtime, within 15m JWT TTL)
        DateTimeOffset slot = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);
        var bookResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = patA.Id,
            DoctorId = docA!.Id,
            CategoryId = catA!.Id,
            StartUtc = slot
        });
        bookResp.EnsureSuccessStatusCode();
        var appt = await bookResp.Content.ReadFromJsonAsync<AppointmentItemViewModel>();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments/{appt!.Id}/status", new TransitionAppointmentStatusDto
        {
            NextStatus = "Arrived"
        })).EnsureSuccessStatusCode();

        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 8, 56, 0, TimeSpan.Zero));
        var startResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits", new StartVisitDto
        {
            AppointmentId = appt.Id,
            IsVisibleToPatient = true
        });
        startResp.EnsureSuccessStatusCode();
        var visitA = await startResp.Content.ReadFromJsonAsync<VisitSessionViewModel>();
        visitA!.SupportsDentalChart.Should().BeTrue();
        visitA.PatientAllergies.Should().ContainSingle(a => a.Allergen == "Penicillin");

        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 9, 9, 0, TimeSpan.Zero));
        var resumeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits", new StartVisitDto
        {
            AppointmentId = appt.Id,
            IsVisibleToPatient = true
        });
        var resumed = await resumeResp.Content.ReadFromJsonAsync<VisitSessionViewModel>();
        resumed!.Id.Should().Be(visitA.Id);
        resumed.ElapsedMinutes.Should().Be(13);
        resumed.OvertimeMinutes.Should().Be(3);

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/vitals", new RecordVisitVitalsDto
        {
            BloodPressureSystolic = 118,
            BloodPressureDiastolic = 76,
            PulseBpm = 72,
            WeightKg = 72m,
            HeightCm = 160m
        })).EnsureSuccessStatusCode();

        var icdList = await clinicAClient.GetFromJsonAsync<List<Icd10CatalogEntryViewModel>>($"/api/v1/clinics/{clinicAId}/clinical/icd10?query=K02");
        icdList.Should().Contain(e => e.Icd10Code == "K02.9");

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/diagnoses", new AddVisitDiagnosisDto
        {
            Icd10Code = "K02.9",
            TitleAr = "تسوس الأسنان",
            TitleEn = "Dental caries",
            IsPrimary = true
        })).EnsureSuccessStatusCode();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/dental-chart", new RecordToothStateDto
        {
            ToothCode = "16",
            NumberingSystem = "FDI",
            Condition = "Caries",
            Surfaces = ["O", "M"]
        })).EnsureSuccessStatusCode();

        var rxBlocked = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/prescriptions", new CreatePrescriptionDto
        {
            AcknowledgeAllergyWarning = false,
            Items =
            [
                new PrescriptionItemDto
                {
                    DrugName = "Amoxicillin 500mg",
                    ActiveIngredient = "Penicillin",
                    Dosage = "500mg",
                    Frequency = "TID",
                    Duration = "7 days"
                }
            ]
        });
        rxBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var rxOkResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/prescriptions", new CreatePrescriptionDto
        {
            AcknowledgeAllergyWarning = true,
            Notes = "With meals",
            Items =
            [
                new PrescriptionItemDto
                {
                    DrugName = "Amoxicillin 500mg",
                    ActiveIngredient = "Penicillin",
                    Dosage = "500mg",
                    Frequency = "TID",
                    Duration = "7 days"
                }
            ]
        });
        rxOkResp.EnsureSuccessStatusCode();
        var rxOk = await rxOkResp.Content.ReadFromJsonAsync<PrescriptionViewModel>();

        var pdfResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/clinical/prescriptions/{rxOk!.Id}/pdf");
        pdfResp.EnsureSuccessStatusCode();
        byte[] pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync();
        string pdfText = Encoding.ASCII.GetString(pdfBytes);
        pdfText.Should().StartWith("%PDF-1.4");
        pdfText.Should().Contain("Amoxicillin 500mg");
        pdfText.Should().Contain(patA.FileNumber);

        var completeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}/complete", new CompleteVisitDto
        {
            IsOvertimeExcused = true,
            OvertimeExcuseReason = "Molar extraction complexity"
        });
        completeResp.EnsureSuccessStatusCode();
        var completed = await completeResp.Content.ReadFromJsonAsync<VisitSessionViewModel>();
        completed!.Status.Should().Be("Completed");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            string visitToken = visitA.Id.ToString();
            int sessionCompletedCount = await db.OutboxMessages
                .CountAsync(m => m.Type == "SessionCompleted" && m.Content.Contains(visitToken));
            sessionCompletedCount.Should().Be(1);
        }

        // Clinical dashboard widgets live aggregates
        var summaryWidget = await clinicAClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/clinical.sessions-summary");
        summaryWidget!.PrimaryValue.Should().BeGreaterThanOrEqualTo(1m);

        // Clinic B (Ophthalmology): JSON-Schema form validation & zero dental tables touched
        var docBResp = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. عيون",
            TitleAr = "استشاري رمد",
            TitleEn = "Ophthalmologist",
            SpecialtyCodes = ["ophthalmology"],
            AssignedRoomIds = [],
            SlotDurationMinutes = 20
        });
        var docB = await docBResp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var patBResp = await clinicBClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicBId}/patients",
            new CreatePatientDto(null, "مريض 7 ب", "01070000002", null, null, 44, "Male", null, null, null, null, false, null));
        var patB = await patBResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        var formCreateResp = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/clinical/forms", new CreateSpecialtyFormDto
        {
            SpecialtyCode = "ophthalmology",
            TitleAr = "قياس ضغط العين",
            TitleEn = "IOP Form",
            JsonSchema = """{"type":"object","required":["iop"],"properties":{"iop":{"type":"number","minimum":5,"maximum":60}}}"""
        });
        formCreateResp.EnsureSuccessStatusCode();
        var formB = await formCreateResp.Content.ReadFromJsonAsync<ClinicalFormViewModel>();

        var visitBResp = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/clinical/visits", new StartVisitDto
        {
            PatientId = patB!.Id,
            DoctorId = docB!.Id,
            SpecialtyCode = "ophthalmology",
            IsVisibleToPatient = true
        });
        visitBResp.EnsureSuccessStatusCode();
        var visitB = await visitBResp.Content.ReadFromJsonAsync<VisitSessionViewModel>();
        visitB!.SupportsDentalChart.Should().BeFalse();

        var invalidFormResp = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/clinical/visits/{visitB.Id}/form-data", new SaveVisitFormDataDto
        {
            ClinicalFormId = formB!.Id,
            AnswersJson = """{"iop":99}"""
        });
        invalidFormResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var validFormResp = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/clinical/visits/{visitB.Id}/form-data", new SaveVisitFormDataDto
        {
            ClinicalFormId = formB.Id,
            AnswersJson = """{"iop":15}"""
        });
        validFormResp.EnsureSuccessStatusCode();

        // Super Admin read-only inspect clinical
        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/clinical");
        inspectResp.EnsureSuccessStatusCode();
        var inspectData = await inspectResp.Content.ReadFromJsonAsync<ClinicClinicalInspectViewModel>();
        inspectData!.Visits.Should().Contain(v => v.Id == visitA.Id);

        var inspectWriteBlocked = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/clinical", new { });
        inspectWriteBlocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Module-off isolation: turn clinical OFF on Clinic A -> clinical returns 403 while scheduling keeps working
        await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = false }]
        });
        var clinicalOffResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/clinical/visits/{visitA.Id}");
        clinicalOffResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var schedStillResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments");
        schedStillResp.EnsureSuccessStatusCode();
    }

    private async Task<string> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        }

        var loginFirst = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "AdminInitial#2026"
        });
        loginFirst.EnsureSuccessStatusCode();
        var firstBody = await loginFirst.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>();

        string totpCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var totpService = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            totpCode = totpService.ComputeCurrentCode(firstBody!.ManualSetupKey!, _clock.UtcNow);
        }

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody!.AccessToken);
        var enrollResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/enroll", new PlatformAdminEnrollDto
        {
            NewPassword = "SuperAdminGate#2026",
            HourlyCode = totpCode
        });
        enrollResp.EnsureSuccessStatusCode();
        var enrolled = await enrollResp.Content.ReadFromJsonAsync<TotpEnrollmentCompletedViewModel>();
        adminClient.DefaultRequestHeaders.Authorization = null;

        return enrolled!.AccessToken;
    }

    private async Task<string> ActivateAndLoginClinicUserAsync(HttpClient clinicClient, string email, string password, string deviceId)
    {
        string code = await ExtractLatestCodeFromOutboxAsync(email);
        var activateResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email,
            Code = code,
            Password = password
        });
        activateResp.EnsureSuccessStatusCode();

        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "GateBrowser"
        });
        loginResp.EnsureSuccessStatusCode();
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        return authBody!.AccessToken;
    }

    private async Task<string> ExtractLatestCodeFromOutboxAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>() as FakeEmailSender
                          ?? throw new InvalidOperationException("FakeEmailSender not registered.");
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        return Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
    }
}

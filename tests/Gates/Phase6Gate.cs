using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase6Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate6_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase6Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 1, 6, 0, 0, TimeSpan.Zero)); // Monday
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
    public async Task Phase6_IntegrationGate_SchedulingQueueAndSignalRIsolation()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        string adminToken = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Owner A (Clinic A) & Owner B (Clinic B)
        string ownerAEmail = $"g6-owner-a-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g6-owner-b-{Guid.NewGuid():N}@clinic.local";

        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner A",
            Email = ownerAEmail,
            MaxClinics = 2
        });
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner B",
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

        var createClinicA = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 6 أ", "UTC", "EGP", "NearestPound", "dev-owner-a"));
        var createClinicB = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 6 ب", "UTC", "EGP", "NearestPound", "dev-owner-b"));
        Guid clinicAId = (await createClinicA.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await createClinicB.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        // Enable patients, scheduling, queue on both clinics
        foreach (Guid cid in new[] { clinicAId, clinicBId })
        {
            var modResp = await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{cid}/modules", new UpdateClinicModulesDto
            {
                StepUpPassword = "SuperAdminGate#2026",
                Modules =
                [
                    new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                    new ModuleToggleItemDto { ModuleKey = "queue", Enabled = true }
                ]
            });
            modResp.EnsureSuccessStatusCode();
        }

        await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/working-hours", new SaveWorkingHoursDto
        {
            DoctorId = null,
            Items = [new WorkingHourItemDto { DayOfWeek = (int)DayOfWeek.Monday, StartTime = "09:00", EndTime = "13:00", IsClosed = false }]
        });

        await clinicBClient.PutAsJsonAsync($"/api/v1/clinics/{clinicBId}/working-hours", new SaveWorkingHoursDto
        {
            DoctorId = null,
            Items = [new WorkingHourItemDto { DayOfWeek = (int)DayOfWeek.Monday, StartTime = "09:00", EndTime = "13:00", IsClosed = false }]
        });

        await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/settings", new UpdateClinicSchedulingSettingsDto
        {
            VipJumpLimit = 1,
            LateGraceMinutes = 10,
            MaxQueuePushes = 1,
            DefaultSlotStepMinutes = 30
        });

        // Two doctors in two specialties + rooms in Clinic A
        var doc1Resp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. أسنان",
            TitleAr = "طبيب",
            TitleEn = "Doctor",
            SpecialtyCodes = ["dental"],
            AssignedRoomIds = [],
            SlotDurationMinutes = 30
        });
        doc1Resp.EnsureSuccessStatusCode();
        var doc1 = await doc1Resp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var doc2Resp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. جلدية",
            TitleAr = "طبيب",
            TitleEn = "Doctor",
            SpecialtyCodes = ["dermatology"],
            AssignedRoomIds = [],
            SlotDurationMinutes = 30
        });
        doc2Resp.EnsureSuccessStatusCode();
        var doc2 = await doc2Resp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var catDental = await (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/categories", new UpsertAppointmentCategoryDto
        {
            SpecialtyCode = "dental",
            NameAr = "فحص أسنان",
            NameEn = "Dental Checkup",
            DefaultDurationMinutes = 30,
            DefaultPriceAmount = 300m
        })).Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();

        var catDerm = await (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/categories", new UpsertAppointmentCategoryDto
        {
            SpecialtyCode = "dermatology",
            NameAr = "كشف جلدية",
            NameEn = "Derm Visit",
            DefaultDurationMinutes = 30,
            DefaultPriceAmount = 350m
        })).Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();

        var p1 = await (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/patients", new CreatePatientDto(null, "مريض أ1", "01020000001", null, null, 30, "Male", null, null, null, null, false, null))).Content.ReadFromJsonAsync<PatientDetailViewModel>();
        var p2 = await (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/patients", new CreatePatientDto(null, "مريض أ2", "01020000002", null, null, 28, "Female", null, null, null, null, false, null))).Content.ReadFromJsonAsync<PatientDetailViewModel>();
        var p3 = await (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/patients", new CreatePatientDto(null, "مريض أ3", "01020000003", null, null, 35, "Male", null, null, null, null, false, null))).Content.ReadFromJsonAsync<PatientDetailViewModel>();

        // 5. Connect SignalR clients to Clinic A and Clinic B
        var clinicAEvents = new ConcurrentBag<ScheduleRealtimeEvent>();
        var clinicBEvents = new ConcurrentBag<ScheduleRealtimeEvent>();

        await using HubConnection hubA = BuildHubConnection(ownerAToken);
        await using HubConnection hubB = BuildHubConnection(ownerBToken);

        hubA.On<ScheduleRealtimeEvent>("PatientArrived", evt => clinicAEvents.Add(evt));
        hubA.On<ScheduleRealtimeEvent>("QueueChanged", evt => clinicAEvents.Add(evt));
        hubA.On<ScheduleRealtimeEvent>("SlotFreed", evt => clinicAEvents.Add(evt));

        hubB.On<ScheduleRealtimeEvent>("PatientArrived", evt => clinicBEvents.Add(evt));
        hubB.On<ScheduleRealtimeEvent>("QueueChanged", evt => clinicBEvents.Add(evt));
        hubB.On<ScheduleRealtimeEvent>("SlotFreed", evt => clinicBEvents.Add(evt));

        await hubA.StartAsync();
        await hubB.StartAsync();

        await hubA.InvokeAsync("JoinClinic", clinicAId);
        await hubB.InvokeAsync("JoinClinic", clinicBId);

        // Verify Client B cannot join Clinic A's SignalR group
        Func<Task> crossJoin = () => hubB.InvokeAsync("JoinClinic", clinicAId);
        await crossJoin.Should().ThrowAsync<HubException>();

        // 6. Free-slot search -> book -> removed from list
        string fromStr = Uri.EscapeDataString("2026-06-01T00:00:00Z");
        string toStr = Uri.EscapeDataString("2026-06-01T23:59:00Z");
        var slotsBefore = await clinicAClient.GetFromJsonAsync<List<FreeSlotItemViewModel>>(
            $"/api/v1/clinics/{clinicAId}/scheduling/free-slots?categoryId={catDental!.Id}&doctorId={doc1!.Id}&fromUtc={fromStr}&toUtc={toStr}");
        slotsBefore.Should().NotBeNullOrEmpty();
        DateTimeOffset slot1 = slotsBefore![0].StartUtc;

        var book1Resp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = p1!.Id,
            DoctorId = doc1.Id,
            CategoryId = catDental.Id,
            StartUtc = slot1,
            Priority = "Normal"
        });
        book1Resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var appt1 = await book1Resp.Content.ReadFromJsonAsync<AppointmentItemViewModel>();

        var slotsAfter = await clinicAClient.GetFromJsonAsync<List<FreeSlotItemViewModel>>(
            $"/api/v1/clinics/{clinicAId}/scheduling/free-slots?categoryId={catDental.Id}&doctorId={doc1.Id}&fromUtc={fromStr}&toUtc={toStr}");
        slotsAfter!.Should().NotContain(s => s.StartUtc == slot1);

        // Second doctor specialty appointment + VIP appointment for doc1
        var book2 = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = p2!.Id,
            DoctorId = doc2!.Id,
            CategoryId = catDerm!.Id,
            StartUtc = slot1,
            Priority = "Normal"
        });
        book2.EnsureSuccessStatusCode();

        var book3 = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = p3!.Id,
            DoctorId = doc1.Id,
            CategoryId = catDental.Id,
            StartUtc = slot1.AddMinutes(30),
            Priority = "Vip"
        });
        book3.EnsureSuccessStatusCode();
        var appt3 = await book3.Content.ReadFromJsonAsync<AppointmentItemViewModel>();

        // Book Normal patient walk-in / appointment to test push-back & VIP jump + Arrival
        await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments/{appt1!.Id}/status", new TransitionAppointmentStatusDto { NextStatus = "Arrived" });
        await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments/{appt3!.Id}/status", new TransitionAppointmentStatusDto { NextStatus = "Arrived" });

        var board = await clinicAClient.GetFromJsonAsync<List<QueueEntryItemViewModel>>($"/api/v1/clinics/{clinicAId}/queue/board?doctorId={doc1.Id}&date=2026-06-01");
        board!.Select(q => q.PatientId).Should().Equal([p3.Id, p1.Id]);

        var pushed = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/queue/entries/{board![0].Id}/push-late", new PushLateQueueEntryDto { MarkNoShowOnExceed = true });
        pushed.EnsureSuccessStatusCode();
        (await pushed.Content.ReadFromJsonAsync<QueueEntryItemViewModel>())!.OrderIndex.Should().Be(2);

        // 3. Cancellation offers slot to waiting list
        DateTimeOffset slot1100 = new(2026, 6, 1, 11, 0, 0, TimeSpan.Zero);
        var bookForCancel = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = p2.Id,
            DoctorId = doc1.Id,
            CategoryId = catDental.Id,
            StartUtc = slot1100
        });
        var apptCancel = await bookForCancel.Content.ReadFromJsonAsync<AppointmentItemViewModel>();

        await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/queue/waiting-list", new AddToWaitingListDto
        {
            PatientId = p3.Id,
            DoctorId = doc1.Id,
            CategoryId = catDental.Id,
            PreferredDate = "2026-06-01"
        });

        await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments/{apptCancel!.Id}/status", new TransitionAppointmentStatusDto
        {
            NextStatus = "Cancelled",
            Reason = "Unable to attend"
        });

        var offers = await clinicAClient.GetFromJsonAsync<List<SlotOfferItemViewModel>>($"/api/v1/clinics/{clinicAId}/queue/slot-offers");
        offers.Should().ContainSingle(o => o.PatientId == p3.Id && o.Status == "Pending");

        // 4. Close day -> affected appointments listed & rescheduled
        var closeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/close-day", new CloseDayDto
        {
            StartDate = "2026-06-01",
            EndDate = "2026-06-01",
            ReasonAr = "صيانة",
            ReasonEn = "Maintenance"
        });
        closeResp.EnsureSuccessStatusCode();
        var closedDay = await closeResp.Content.ReadFromJsonAsync<CloseDayViewModel>();
        closedDay!.AffectedAppointments.Should().NotBeEmpty();

        Guid affectedId = closedDay.AffectedAppointments[0].Id;
        var reschResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments/{affectedId}/reschedule", new RescheduleAppointmentDto
        {
            PresetKey = "after_week"
        });
        reschResp.EnsureSuccessStatusCode();

        // Give SignalR a short moment to flush all events
        await Task.Delay(250);
        clinicAEvents.Should().Contain(e => e.EventType == "PatientArrived");
        clinicAEvents.Should().Contain(e => e.EventType == "QueueChanged");
        clinicAEvents.Should().Contain(e => e.EventType == "SlotFreed");
        clinicBEvents.Should().BeEmpty();

        // 7. Module-off resilience: disable queue -> widgets disappear while scheduling continues working
        await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "queue", Enabled = false }]
        });

        var catalogResp = await clinicAClient.GetFromJsonAsync<DashboardWidgetCatalogViewModel>($"/api/v1/clinics/{clinicAId}/dashboards/widgets");
        catalogResp!.Widgets.Should().Contain(w => w.ModuleKey == "scheduling");
        catalogResp.Widgets.Should().NotContain(w => w.ModuleKey == "queue");
    }

    private HubConnection BuildHubConnection(string bearerToken)
    {
        return new HubConnectionBuilder()
            .WithUrl("http://app.clinic.localhost/hubs/schedule", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(bearerToken);
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("Host", "app.clinic.localhost");
            })
            .Build();
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

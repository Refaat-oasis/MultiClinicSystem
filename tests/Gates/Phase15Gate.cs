using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase15Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate15_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase15Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero));
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
    public async Task Phase15IntegrationGate_TwoDevicesOfflineReconnect_ReceiptBlocksFefoConflictsIsolationAndModuleOff()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        using var clinicClient1 = _factory.CreateClient();
        clinicClient1.DefaultRequestHeaders.Host = "app.clinic.localhost";
        using var clinicClient2 = _factory.CreateClient();
        clinicClient2.DefaultRequestHeaders.Host = "app.clinic.localhost";

        var (adminToken, _) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerEmail = $"g15-owner-{Guid.NewGuid():N}@clinic.local";
        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner Gate 15",
            Email = ownerEmail,
            MaxClinics = 3
        })).EnsureSuccessStatusCode();

        const string device1 = "gate15-device-1";
        const string device2 = "gate15-device-2";
        string token1 = await ActivateAndLoginClinicUserAsync(clinicClient1, ownerEmail, "OwnerGate15#Pass", device1);
        clinicClient1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        clinicClient1.DefaultRequestHeaders.Add("X-Device-Id", device1);

        var login2Resp = await clinicClient2.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = ownerEmail,
            Password = "OwnerGate15#Pass",
            DeviceId = device2,
            DeviceName = "Gate15Tablet2"
        });
        login2Resp.EnsureSuccessStatusCode();
        var login2Body = await login2Resp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        clinicClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login2Body!.AccessToken);
        clinicClient2.DefaultRequestHeaders.Add("X-Device-Id", device2);

        using (var approveScope = _factory.Services.CreateScope())
        {
            var db = approveScope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var devRec = await db.AppDevices.FirstAsync(d => d.DeviceId == device2);
            devRec.Approve(_clock.UtcNow);
            await db.SaveChangesAsync();
        }

        var createAResp = await clinicClient1.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة بوابة ١٥ أ", "UTC", "EGP", "None", device1));
        createAResp.EnsureSuccessStatusCode();
        Guid clinicA = (await createAResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await OnboardClinicAsync(clinicClient1, clinicA, $"g15-a-{clinicA:N}"[..16]);

        var createBResp = await clinicClient1.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة بوابة ١٥ ب", "UTC", "EGP", "None", device1));
        createBResp.EnsureSuccessStatusCode();
        Guid clinicB = (await createBResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await OnboardClinicAsync(clinicClient1, clinicB, $"g15-b-{clinicB:N}"[..16]);

        string[] modulesA = ["patients", "scheduling", "clinical", "billing", "inventory", "finance", "offline-sync"];
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = modulesA.Select(m => new ModuleToggleItemDto { ModuleKey = m, Enabled = true, Note = "Gate15" }).ToList()
        })).EnsureSuccessStatusCode();

        // Refresh tokens with clinic A active before checking Clinic B module-off vs tenant switch
        clinicClient1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/switch", new { })).Content.ReadFromJsonAsync<SwitchClinicViewModel>())!.AccessToken);
        clinicClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await (await clinicClient2.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/switch", new { })).Content.ReadFromJsonAsync<SwitchClinicViewModel>())!.AccessToken);

        // Clinic B has offline-sync OFF -> 403 MODULE_DISABLED
        var offRes = await clinicClient1.GetAsync($"/api/v1/clinics/{clinicB}/sync/settings");
        offRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Pre-allocate receipt blocks for Device 1 & Device 2
        var b1 = await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/sync/number-blocks", new AllocateNumberBlockDto(device1, "Receipt", 50))).Content.ReadFromJsonAsync<AllocatedNumberBlockViewModel>();
        var b2 = await (await clinicClient2.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/sync/number-blocks", new AllocateNumberBlockDto(device2, "Receipt", 50))).Content.ReadFromJsonAsync<AllocatedNumberBlockViewModel>();
        b1!.ToNumber.Should().BeLessThan(b2!.FromNumber);

        // Setup WorkingHours, Room, Doctor, Category, Supplier & FEFO Inventory Item online before disconnecting
        (await clinicClient1.PutAsJsonAsync($"/api/v1/clinics/{clinicA}/working-hours", new SaveWorkingHoursDto
        {
            Items = Enumerable.Range(0, 7).Select(d => new WorkingHourItemDto { DayOfWeek = d, StartTime = "08:00", EndTime = "18:00", IsClosed = false }).ToList()
        })).EnsureSuccessStatusCode();
        Guid roomId = (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/rooms",
            new CreateClinicRoomDto { Code = "G15-R1", NameAr = "غرفة ١٥", NameEn = "Room 15", SpecialtyCode = "dental" })).Content.ReadFromJsonAsync<ClinicRoomViewModel>())!.Id;
        Guid docId = (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/doctors",
            new CreateClinicDoctorDto { FullName = "د. عادل رمزي", TitleAr = "استشاري", TitleEn = "Consultant", SpecialtyCodes = ["dental"], AssignedRoomIds = [roomId], SlotDurationMinutes = 30 })).Content.ReadFromJsonAsync<ClinicDoctorViewModel>())!.Id;
        Guid catId = (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/scheduling/categories",
            new UpsertAppointmentCategoryDto { SpecialtyCode = "dental", NameAr = "كشف", NameEn = "Consultation", DefaultDurationMinutes = 30, DefaultPriceAmount = 500m, DefaultPriceCurrency = "EGP", BookableOnline = true })).Content.ReadFromJsonAsync<AppointmentCategoryViewModel>())!.Id;

        Guid supId = (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/billing/suppliers",
            new CreateSupplierDto(null, "شركة النيل", SupplierType.MedicalSupplies, "01099990000", "Medical", null))).Content.ReadFromJsonAsync<SupplierViewModel>())!.Id;
        Guid itemId = (await (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/inventory/items",
            new UpsertInventoryItemDto
            {
                Code = "SYR-15",
                NameAr = "سرنجة معقمة",
                NameEn = "Sterile Syringe",
                UnitName = "piece",
                PackSize = 20,
                UnitCostAmount = 5m,
                DefaultMarkupPercent = 20m,
                LowStockThreshold = 5m,
                SpecialtyCode = "dental",
                StorageLocation = "A1",
                IsActive = true
            })).Content.ReadFromJsonAsync<InventoryItemViewModel>())!.Id;

        (await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/inventory/purchase-invoices",
            new ReceivePurchaseInvoiceDto
            {
                SupplierId = supId,
                SupplierName = "شركة النيل",
                InvoiceNumber = "INV-G15-1",
                InvoiceDate = new DateOnly(2026, 10, 15),
                PaidAmount = 0m,
                PaymentMethod = null,
                Notes = "FEFO batch",
                Lines =
                [
                    new PurchaseInvoiceLineDto
                    {
                        ItemId = itemId,
                        PackSize = 20,
                        PackCount = 1,
                        PackPriceAmount = 100m,
                        BatchNumber = "BATCH-G15-EARLY",
                        ExpiryDate = new DateOnly(2026, 11, 1)
                    }
                ],
                OperationId = GuidV7.NewGuid()
            })).EnsureSuccessStatusCode();

        // Device 1 & Device 2 reconnect after offline queue buildup
        Guid pat1 = GuidV7.NewGuid();
        Guid pat2 = GuidV7.NewGuid();
        Guid visit1 = GuidV7.NewGuid();
        Guid charge1 = GuidV7.NewGuid();
        Guid charge2 = GuidV7.NewGuid();
        DateTimeOffset clashSlot = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

        var push1 = await clinicClient1.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/sync/push", new SyncPushBatchDto(device1,
        [
            new SyncPushOperationDto(GuidV7.NewGuid(), "RegisterPatient", "Patient", pat1,
                JsonSerializer.Serialize(new { clientPatientId = pat1, fullName = "مريض ١", phone = "01011110001", gender = "Male", confirmDuplicateCreation = true }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "BookAppointment", "Appointment", GuidV7.NewGuid(),
                JsonSerializer.Serialize(new { patientId = pat1, doctorId = docId, roomId, categoryId = catId, startUtc = clashSlot, priority = "Normal" }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "StartVisit", "Visit", visit1,
                JsonSerializer.Serialize(new { clientVisitId = visit1, patientId = pat1, doctorId = docId, specialtyCode = "dental", roomId, plannedDurationMinutes = 30, isVisibleToPatient = true, categoryId = catId }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "CompleteVisit", "Visit", visit1,
                JsonSerializer.Serialize(new { visitId = visit1, isOvertimeExcused = false, toolsUsed = new[] { new { inventoryItemId = itemId, quantity = 4m } } }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "CreateCharge", "Charge", charge1,
                JsonSerializer.Serialize(new { id = charge1, patientId = pat1, visitId = visit1, doctorId = docId, standardSessionPriceAmount = 500m, isFreeSession = false, postImmediately = true }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "RecordPayment", "Payment", GuidV7.NewGuid(),
                JsonSerializer.Serialize(new { patientId = pat1, chargeId = charge1, isDeposit = false, splits = new[] { new { method = 0, amount = 500m } } }), _clock.UtcNow)
        ]));
        push1.EnsureSuccessStatusCode();
        var push1Vm = await push1.Content.ReadFromJsonAsync<SyncPushBatchViewModel>();
        push1Vm!.AppliedCount.Should().Be(6, string.Join(", ", push1Vm.Outcomes.Where(o => o.ErrorCode != null).Select(o => $"{o.ActionType}:{o.ErrorCode}:{o.ErrorMessage}")));

        var push2 = await clinicClient2.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/sync/push", new SyncPushBatchDto(device2,
        [
            new SyncPushOperationDto(GuidV7.NewGuid(), "RegisterPatient", "Patient", pat2,
                JsonSerializer.Serialize(new { clientPatientId = pat2, fullName = "مريض ٢", phone = "01011110002", gender = "Female", confirmDuplicateCreation = true }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "BookAppointment", "Appointment", GuidV7.NewGuid(),
                JsonSerializer.Serialize(new { patientId = pat2, doctorId = docId, roomId, categoryId = catId, startUtc = clashSlot, priority = "Normal" }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "CreateCharge", "Charge", charge2,
                JsonSerializer.Serialize(new { id = charge2, patientId = pat2, doctorId = docId, standardSessionPriceAmount = 300m, isFreeSession = false, postImmediately = true }), _clock.UtcNow),
            new SyncPushOperationDto(GuidV7.NewGuid(), "RecordPayment", "Payment", GuidV7.NewGuid(),
                JsonSerializer.Serialize(new { patientId = pat2, chargeId = charge2, isDeposit = false, splits = new[] { new { method = 0, amount = 300m } } }), _clock.UtcNow)
        ]));
        push2.EnsureSuccessStatusCode();
        var push2Vm = await push2.Content.ReadFromJsonAsync<SyncPushBatchViewModel>();
        push2Vm!.AppliedCount.Should().Be(3);
        push2Vm.ConflictCount.Should().Be(1);

        string receipt1 = push1Vm.Outcomes[^1].AssignedReceiptNumbers!.Single();
        string receipt2 = push2Vm.Outcomes[^1].AssignedReceiptNumbers!.Single();
        receipt1.Should().NotBe(receipt2);

        var items = await clinicClient1.GetFromJsonAsync<List<InventoryItemViewModel>>($"/api/v1/clinics/{clinicA}/inventory/items");
        items!.Single(i => i.Id == itemId).QuantityOnHand.Should().Be(16m);

        var closingPreview = await clinicClient1.GetFromJsonAsync<DailyClosingViewModel>(
            $"/api/v1/clinics/{clinicA}/billing/closing?date=2026-10-15");
        closingPreview!.TotalExpectedAmount.Should().Be(800m);

        var conflicts = await clinicClient1.GetFromJsonAsync<List<SyncConflictViewModel>>($"/api/v1/clinics/{clinicA}/sync/conflicts");
        conflicts!.Should().ContainSingle(c => c.Status == SyncConflictStatus.Open);

        var resolveResp = await clinicClient1.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/sync/conflicts/{conflicts[0].Id}/resolve",
            new ResolveSyncConflictDto(SyncConflictStatus.ResolvedKeepServer, "Resolved at reception"));
        resolveResp.EnsureSuccessStatusCode();

        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicA}/inspect/offline-sync");
        inspectResp.EnsureSuccessStatusCode();
    }

    private static async Task OnboardClinicAsync(HttpClient clinicClient, Guid clinicId, string slug)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("عيادة بوابة ١٥", "12 شارع التحرير", "Cairo", "Cairo", "01000000015", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto([new SpecialtySelectionDto("dental", null, null)]))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/slug",
            new OnboardingSlugDto(slug))).EnsureSuccessStatusCode();
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/complete", null)).EnsureSuccessStatusCode();
    }

    private async Task<(string Token, string ManualSetupKey)> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
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

        return (enrolled!.AccessToken, firstBody.ManualSetupKey!);
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

        clinicClient.DefaultRequestHeaders.Authorization = null;
        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "Gate15Browser"
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

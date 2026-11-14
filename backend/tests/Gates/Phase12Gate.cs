using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class Phase12Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate12_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase12Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));
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
    public async Task Phase12_IntegrationGate_PackPurchasesFefoConsumptionLowStockAlertsWidgetsAndModuleOff()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        (string adminToken, _) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerEmail = $"g12-owner-{Guid.NewGuid():N}@clinic.local";
        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G12",
            Email = ownerEmail,
            MaxClinics = 3
        })).EnsureSuccessStatusCode();

        using var clinicClient = _factory.CreateClient();
        clinicClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerToken = await ActivateAndLoginClinicUserAsync(clinicClient, ownerEmail, "OwnerPass#2026", "dev-owner-g12");
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        clinicClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-g12");

        // Clinic A (inventory ON) and Clinic B (inventory OFF)
        var cA = await (await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 12 Clinic A", "UTC", "EGP", "None", "dev-owner-g12")))
            .Content.ReadFromJsonAsync<MyClinicCardViewModel>();
        Guid clinicA = cA!.ClinicId;
        await CompleteOnboardingAsync(clinicClient, clinicA, $"g12-a-{clinicA:N}"[..16], ["dental"]);

        var cB = await (await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 12 Clinic B", "UTC", "EGP", "None", "dev-owner-g12")))
            .Content.ReadFromJsonAsync<MyClinicCardViewModel>();
        Guid clinicB = cB!.ClinicId;
        await CompleteOnboardingAsync(clinicClient, clinicB, $"g12-b-{clinicB:N}"[..16], ["dental"]);

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "finance", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "inventory", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicB}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "inventory", Enabled = false }
            ]
        })).EnsureSuccessStatusCode();

        // Criterion 1: Buy box of 30 (pack size edited to 25), stock increases, supplier owes remainder
        var supplier = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/billing/suppliers",
            new CreateSupplierDto(null, "شركة النيل للمستلزمات", SupplierType.MedicalSupplies, "01009998887", "Dental consumables", null)))
            .Content.ReadFromJsonAsync<SupplierViewModel>();

        var item = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/inventory/items",
            new UpsertInventoryItemDto
            {
                Code = "COMP-30",
                NameAr = "حشوة كومبوزيت",
                NameEn = "Composite Syringe",
                UnitName = "capsule",
                PackSize = 30,
                UnitCostAmount = 20m,
                DefaultMarkupPercent = 25m,
                LowStockThreshold = 10m,
                SpecialtyCode = "dental",
                StorageLocation = "Cabinet B",
                IsActive = true
            }))
            .Content.ReadFromJsonAsync<InventoryItemViewModel>();

        var invoiceResp = await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/inventory/purchase-invoices",
            new ReceivePurchaseInvoiceDto
            {
                SupplierId = supplier!.Id,
                SupplierName = supplier.Name,
                InvoiceNumber = "G12-INV-001",
                InvoiceDate = new DateOnly(2026, 9, 1),
                PaidAmount = 200m,
                PaymentMethod = PaymentMethod.Cash,
                Notes = "Pack size edited from 30 to 25",
                Lines =
                [
                    new PurchaseInvoiceLineDto
                    {
                        ItemId = item!.Id,
                        PackSize = 25,
                        PackCount = 1,
                        PackPriceAmount = 750m,
                        BatchNumber = "LOT-G12-A",
                        ExpiryDate = new DateOnly(2026, 9, 15)
                    }
                ]
            });
        invoiceResp.EnsureSuccessStatusCode();
        var invoice = await invoiceResp.Content.ReadFromJsonAsync<PurchaseInvoiceViewModel>();
        invoice!.TotalAmount.Should().Be(750m);
        invoice.PaidAmount.Should().Be(200m);
        invoice.RemainingAmount.Should().Be(550m);

        var stmt = await clinicClient.GetFromJsonAsync<SupplierInventoryStatementViewModel>(
            $"/api/v1/clinics/{clinicA}/inventory/suppliers/{supplier.Id}/statement");
        stmt!.BalanceDueAmount.Should().Be(550m);
        stmt.FinanceLedgerBalanceDueAmount.Should().Be(550m);

        // Criterion 2: Visit uses 16 capsules -> stock drops once (9 left <= 10 threshold), charge uses 25% markup (30 cost -> 37.50 patient unit), low-stock alert arrives
        var roomA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/rooms",
            new CreateClinicRoomDto { Code = "G12-R1", NameAr = "غرفة 1", NameEn = "Room 1", SpecialtyCode = "dental" }))
            .Content.ReadFromJsonAsync<ClinicRoomViewModel>();
        var docA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/doctors",
            new CreateClinicDoctorDto { FullName = "د. كريم وائل", TitleAr = "أخصائي", TitleEn = "Specialist", SpecialtyCodes = ["dental"], AssignedRoomIds = [roomA!.Id], SlotDurationMinutes = 30 }))
            .Content.ReadFromJsonAsync<ClinicDoctorViewModel>();
        var patA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/patients",
            new CreatePatientDto(null, "وليد سمير", "01094000001", null, new DateOnly(1990, 6, 5), null, "Male", null, null, null, "dental", false, null)))
            .Content.ReadFromJsonAsync<PatientDetailViewModel>();

        var visitA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/clinical/visits",
            new StartVisitDto { PatientId = patA!.Id, DoctorId = docA!.Id, SpecialtyCode = "dental", RoomId = roomA.Id, PlannedDurationMinutes = 30 }))
            .Content.ReadFromJsonAsync<VisitSessionViewModel>();

        (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/clinical/visits/{visitA!.Id}/complete",
            new CompleteVisitDto
            {
                OperationId = GuidV7.NewGuid(),
                ToolsUsed = [new VisitToolUsageDto { InventoryItemId = item.Id, Quantity = 16m }]
            })).EnsureSuccessStatusCode();

        var updatedItemsA = await clinicClient.GetFromJsonAsync<IReadOnlyList<InventoryItemViewModel>>($"/api/v1/clinics/{clinicA}/inventory/items");
        updatedItemsA!.Single(i => i.Id == item.Id).QuantityOnHand.Should().Be(9m);

        var chargesA = await clinicClient.GetFromJsonAsync<IReadOnlyList<ChargeViewModel>>($"/api/v1/clinics/{clinicA}/billing/charges?patientId={patA.Id}");
        var chargeA = chargesA!.Single(c => c.VisitId == visitA.Id);
        chargeA.ToolsTotalCostAmount.Should().Be(480m);     // 16 * 30
        chargeA.ToolsTotalChargedAmount.Should().Be(600m);  // 480 * 1.25
        chargeA.ToolsMarkupAmount.Should().Be(120m);

        var alertsA = await clinicClient.GetFromJsonAsync<IReadOnlyList<InventoryAlertViewModel>>($"/api/v1/clinics/{clinicA}/inventory/alerts");
        alertsA!.Should().Contain(a => a.Kind == "LowStock" && a.ItemId == item.Id);

        // Criterion 3: Clinic B has Inventory OFF -> visit with billing still works cleanly, inventory endpoint blocked
        var offResp = await clinicClient.GetAsync($"/api/v1/clinics/{clinicB}/inventory/items");
        offResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var roomB = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicB}/rooms",
            new CreateClinicRoomDto { Code = "G12-B-R1", NameAr = "غرفة ب", NameEn = "Room B", SpecialtyCode = "dental" }))
            .Content.ReadFromJsonAsync<ClinicRoomViewModel>();
        var docB = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicB}/doctors",
            new CreateClinicDoctorDto { FullName = "د. نهى", TitleAr = "أخصائية", TitleEn = "Specialist", SpecialtyCodes = ["dental"], AssignedRoomIds = [roomB!.Id], SlotDurationMinutes = 30 }))
            .Content.ReadFromJsonAsync<ClinicDoctorViewModel>();
        var patB = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicB}/patients",
            new CreatePatientDto(null, "دينا علي", "01094000002", null, new DateOnly(1994, 3, 15), null, "Female", null, null, null, "dental", false, null)))
            .Content.ReadFromJsonAsync<PatientDetailViewModel>();
        var visitB = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicB}/clinical/visits",
            new StartVisitDto { PatientId = patB!.Id, DoctorId = docB!.Id, SpecialtyCode = "dental", RoomId = roomB.Id, PlannedDurationMinutes = 30 }))
            .Content.ReadFromJsonAsync<VisitSessionViewModel>();
        (await clinicClient.PostAsJsonAsync($"/api/v1/clinics/{clinicB}/clinical/visits/{visitB!.Id}/complete", new CompleteVisitDto()))
            .EnsureSuccessStatusCode();
        var chargesB = await clinicClient.GetFromJsonAsync<IReadOnlyList<ChargeViewModel>>($"/api/v1/clinics/{clinicB}/billing/charges?patientId={patB.Id}");
        chargesB!.Should().ContainSingle(c => c.VisitId == visitB.Id);

        // Criterion 4: Dashboard widgets match source data & disappear cleanly when inventory is switched off
        var usageWidget = await clinicClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicA}/dashboards/widgets/inventory.usage-by-item");
        usageWidget!.PrimaryValue.Should().Be(16m);

        var stockValueWidget = await clinicClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicA}/dashboards/widgets/inventory.stock-value");
        stockValueWidget!.PrimaryValue.Should().Be(270m); // 9 * 30

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "inventory", Enabled = false }]
        })).EnsureSuccessStatusCode();

        var catalogAfterOff = await clinicClient.GetFromJsonAsync<DashboardWidgetCatalogViewModel>(
            $"/api/v1/clinics/{clinicA}/dashboards/widgets");
        catalogAfterOff!.Widgets.Should().NotContain(w => w.ModuleKey == "inventory");

        var billingChargesStillWork = await clinicClient.GetAsync($"/api/v1/clinics/{clinicA}/billing/charges?patientId={patA.Id}");
        billingChargesStillWork.EnsureSuccessStatusCode();

        // Super admin inspect snapshot while module re-enabled
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "inventory", Enabled = true }]
        })).EnsureSuccessStatusCode();

        var inspectInv = await adminClient.GetFromJsonAsync<ClinicInventoryInspectViewModel>(
            $"/api/v1/platform/clinics/{clinicA}/inspect/inventory");
        inspectInv!.Items.Should().ContainSingle(i => i.ItemCode == "COMP-30");
    }

    private static async Task CompleteOnboardingAsync(HttpClient clinicClient, Guid clinicId, string slug, string[] specialties)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("Gate 12 Clinic", "10 Nile St", "Maadi", "Cairo", "01001112233", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto(specialties.Select(s => new SpecialtySelectionDto(s, null, null)).ToList()))).EnsureSuccessStatusCode();
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
            DeviceName = "Gate12Browser"
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
